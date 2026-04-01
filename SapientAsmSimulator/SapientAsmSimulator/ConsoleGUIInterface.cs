// Crown-owned copyright, 2021-2025

namespace SapientASMsimulator
{
    using log4net;

    /// <summary>
    /// Headless implementation of IGUIInterface that writes to log4net / stdout.
    /// Replaces the WinForms ClientForm for Docker / Linux deployments.
    /// </summary>
    public class ConsoleGUIInterface : IGUIInterface
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ConsoleGUIInterface));

        /// <inheritdoc/>
        public void UpdateOutputText(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Log.Info(message);
            }
        }

        /// <inheritdoc/>
        public void UpdateASMText(string text)
        {
            Log.InfoFormat("ASM ID: {0}", text);
        }
    }
}
