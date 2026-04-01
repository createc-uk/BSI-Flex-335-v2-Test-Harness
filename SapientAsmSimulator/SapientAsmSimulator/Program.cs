// File:              $Workfile: Program.cs$
// <copyright file="Program.cs" >
// Crown-owned copyright, 2021-2025
// See Release/Supply Conditions
// </copyright>

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace SapientASMsimulator
{
    using System.Threading;
    using log4net;

    /// <summary>
    /// Headless entry point for Docker / Linux deployments.
    /// Replaces the WinForms application loop with a console-friendly blocking loop.
    /// </summary>
    public static class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">
        /// Optional: args[0] = port override, args[1] = ASM ID override
        /// </param>
        public static void Main(string[] args)
        {
            const int ExeName = 0;
            const int ExeVersion = 2;

            string[] assemblyDetails = System.Reflection.Assembly.GetExecutingAssembly().FullName!.Split(',', '=');
            Log.Info(Environment.NewLine);
            Log.Info(assemblyDetails[ExeName] + " - Version " + assemblyDetails[ExeVersion]);

            ASMMainProcess.AsmId = Properties.Settings.Default.FixedASMId;

            if (args.Length > 0)
            {
                ASMMainProcess.PortId = args[0];
            }

            if (args.Length > 1)
            {
                ASMMainProcess.AsmId = args[1];
            }

            if (string.IsNullOrEmpty(ASMMainProcess.AsmId))
            {
                ASMMainProcess.AsmId = Ulid.NewUlid().ToString();
            }

            var gui = new ConsoleGUIInterface();
            var mainProcess = new ASMMainProcess(gui);

            // Mirror what ClientForm set before the user clicked "Send Registration"
            SapientASMsimulator.Common.BaseGenerators.ASMId = ASMMainProcess.AsmId;
            SapientASMsimulator.Common.RegistrationGenerator.AsmId = ASMMainProcess.AsmId;

            mainProcess.Initialise();
            mainProcess.SendRegistration();
            mainProcess.SetHeartbeatLoopState(true);
            mainProcess.SendHeartbeatLoop();
            mainProcess.SetDetectionLoopState(true);
            mainProcess.SendDetectionLoop();

            Log.InfoFormat("ASM simulator running. ASM ID: {0}", ASMMainProcess.AsmId);

            var exitEvent = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; exitEvent.Set(); };
            AppDomain.CurrentDomain.ProcessExit += (_, _) => exitEvent.Set();
            exitEvent.Wait();

            mainProcess.Shutdown();
        }
    }
}
