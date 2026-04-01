// Project:           $Project: 00010855-Sapient$
// File:              $Workfile: Program.cs$
// Crown-owned copyright, 2021-2025
//  See Release/Supply Conditions

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace SapientDmmSimulator
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Timers;
    using Google.Protobuf.WellKnownTypes;
    using log4net;
    using Sapient.Data;
    using SAPIENT.ReadSampleMessage;
    using SapientDmmSimulator.Common;
    using SapientServices;
    using SapientServices.Communication;

    /// <summary>
    /// Headless entry point for Docker / Linux deployments.
    /// Replicates TaskForm startup and message handling without Windows Forms.
    /// </summary>
    public class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        private static IConnection? messenger;
        private static SapientLogger? sapientLogger;

        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        public static void Main()
        {
            const int ExeName = 0;
            const int ExeVersion = 2;

            string[] assemblyDetails = System.Reflection.Assembly.GetExecutingAssembly().FullName!.Split(',', '=');
            Log.Info(Environment.NewLine);
            Log.Info(assemblyDetails[ExeName] + " - Version " + assemblyDetails[ExeVersion]);

            string logDirectory = Properties.Settings.Default.LogDirectory;
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            SetLogPath(logDirectory);

            if (Properties.Settings.Default.Log)
            {
                sapientLogger = SapientLogger.CreateLogger(
                    Properties.Settings.Default.LogDirectory,
                    Properties.Settings.Default.LogPrefix,
                    Properties.Settings.Default.IncrementIntervalSeconds);
            }

            var client = new SapientClient(
                Properties.Settings.Default.DmmDataAgentAddress,
                Properties.Settings.Default.DmmDataAgentPort);

            ICommsConnection connection = client;
            messenger = client;
            messenger.SetNoDelay(true);
            messenger.ValidationEnabled = Properties.Settings.Default.ValidationEnabled;
            messenger.MessageSent += MessengerMessageSent;
            messenger.MessageReceived += MessengerMessageReceived;
            messenger.MessageError += MessengerMessageError;
            messenger.SendErrorMessage += MessengerSendErrorMessage;

            const bool sendOnlyConnection = false;
            connection.Start(1024 * 1024, sendOnlyConnection);
            connection.SetDataReceivedCallback(DataCallback);

            Log.InfoFormat("DMM simulator connected to port: {0}", Properties.Settings.Default.DmmDataAgentPort);

            // Send a periodic DMM heartbeat every 10 seconds
            var heartbeatTimer = new System.Timers.Timer(10000);
            heartbeatTimer.Elapsed += (_, _) =>
            {
                try
                {
                    var hb = new HeartbeatGenerator();
                    hb.GenerateHLStatus(messenger);
                }
                catch (Exception ex)
                {
                    Log.Error("Heartbeat error: " + ex.Message);
                }
            };
            heartbeatTimer.Start();

            var exitEvent = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; exitEvent.Set(); };
            AppDomain.CurrentDomain.ProcessExit += (_, _) => exitEvent.Set();
            exitEvent.Wait();

            heartbeatTimer.Stop();
            connection.Shutdown();
        }

        private static void DataCallback(SapientMessage message, IConnection client)
        {
            if (message == null)
            {
                return;
            }

            switch (message.ContentCase)
            {
                case SapientMessage.ContentOneofCase.Registration:
                    try
                    {
                        Log.InfoFormat("Registration Message Received: {0}", message.NodeId);
                        GenerateRegistrationAck(message);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Parse Registration Message Failed ", ex);
                    }

                    break;
                case SapientMessage.ContentOneofCase.StatusReport:
                    Log.DebugFormat("Status Report Message Received: {0}", message.NodeId);
                    break;
                case SapientMessage.ContentOneofCase.DetectionReport:
                    Log.DebugFormat("Detection Report Message Received: {0}", message.NodeId);
                    break;
                case SapientMessage.ContentOneofCase.Task:
                    try
                    {
                        Log.Info("Task Message Received");
                        var task = message.Task;
                        GenerateTaskAck(message.NodeId, task.TaskId);
                        Log.InfoFormat("Task ID: {0}", task.TaskId);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Parse Task Message Failed ", ex);
                    }

                    break;
                case SapientMessage.ContentOneofCase.TaskAck:
                    try
                    {
                        var taskAck = message.TaskAck;
                        Log.InfoFormat("SensorTaskACK Task ID: {0}:{1}:{2}", taskAck.TaskId, taskAck.TaskStatus, taskAck.Reason);
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorFormat("Parse Task ACK Failed: {0} {1}", message?.ContentCase, ex);
                    }

                    break;
                case SapientMessage.ContentOneofCase.Alert:
                    Log.DebugFormat("Alert Message Received: {0}", message.NodeId);
                    break;
                default:
                    Log.InfoFormat("Message Received: {0}", message.ContentCase);
                    break;
            }
        }

        private static void GenerateRegistrationAck(SapientMessage registrationMessage)
        {
            if (registrationMessage == null || messenger == null)
            {
                return;
            }

            var readSampleMessage = new ReadSampleMessage();
            string error = string.Empty;
            SapientMessage? ack = readSampleMessage.ReadSampleMessageFromFile(
                SapientMessage.ContentOneofCase.RegistrationAck, string.Empty, out error);

            if (ack != null)
            {
                ack.NodeId = Properties.Settings.Default.FixedDMMId;
                ack.DestinationId = registrationMessage.NodeId;
                ack.Timestamp = Timestamp.FromDateTime(DateTime.Now.ToUniversalTime());

                if (messenger.SendMessage(ack))
                {
                    Log.InfoFormat("Send Registration Ack: {0}", ack.DestinationId);
                }
                else
                {
                    Log.ErrorFormat("Send Registration Ack Failed: {0}", ack.DestinationId);
                }
            }
            else
            {
                Log.Error("Failed to create RegistrationAck: " + error);
            }
        }

        private static void GenerateTaskAck(string sensorId, string taskId)
        {
            if (messenger == null)
            {
                return;
            }

            var message = new SapientMessage
            {
                Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                NodeId = Properties.Settings.Default.FixedDMMId,
                DestinationId = sensorId,
                TaskAck = new TaskAck
                {
                    TaskId = taskId,
                    TaskStatus = TaskAck.Types.TaskStatus.Accepted,
                },
            };

            Log.Info(messenger.SendMessage(message) ? "Send Task Ack Succeeded" : "Send Task Ack Failed");
        }

        private static void MessengerMessageSent(object? sender, SapientMessageEventArgs e)
        {
            Log.InfoFormat("Send {0}", e?.Message?.ContentCase);
        }

        private static void MessengerMessageReceived(object? sender, SapientMessageEventArgs e)
        {
            Log.InfoFormat("Received {0}", e?.Message?.ContentCase);
        }

        private static void MessengerMessageError(object? sender, SapientMessageEventArgs e)
        {
            Log.ErrorFormat("Error in {0}: {1}", e?.Message?.ContentCase, e?.Error);
        }

        private static void MessengerSendErrorMessage(object? sender, SapientMessageEventArgs e)
        {
            if (e?.Message != null && messenger != null)
            {
                e.Message.NodeId = Properties.Settings.Default.FixedDMMId;
                messenger.SendMessage(e.Message);
            }
        }

        private static void SetLogPath(string logDirectory)
        {
            var hierarchy = (log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository();
            foreach (var appender in hierarchy.Root.Appenders)
            {
                if (appender is log4net.Appender.FileAppender fileAppender)
                {
                    fileAppender.File = Path.Combine(logDirectory, Path.GetFileName(fileAppender.File));
                    fileAppender.ActivateOptions();
                }
            }
        }
    }
}

