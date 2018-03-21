using PokemonGoGUI.Enums;
using PokemonGoGUI.GoManager.Models;
using System;

namespace PokemonGoGUI.GoManager
{
    public partial class Manager :IDisposable
    {
        public delegate void LoggerHandler(object sender, LoggerEventArgs e);
        public event LoggerHandler OnLog;

        public void LogCaller(LoggerEventArgs args)
        {
            string eMessage = String.Empty;

            if(args.Exception != null)
            {
                eMessage = args.Exception.Message;
            }

            AddLog(new Log(args.LogType, args.Message, (Exception)args.Exception));

            OnLog?.Invoke(this, args);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            _client.Dispose();
            _pauser.Dispose();
        }
    }

    public class LoggerEventArgs : EventArgs
    {
        public LoggerTypes LogType { get; private set; }
        public string Message { get; private set; }
        public Exception Exception { get; private set; }

        public LoggerEventArgs(string message, LoggerTypes logtype, Exception exception = null)
        {
            LogType = logtype;
            Message = message;
            Exception = exception;
        }
    }
}
