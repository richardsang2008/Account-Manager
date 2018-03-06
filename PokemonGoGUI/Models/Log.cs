using PokemonGoGUI.Enums;
using System;
using System.Drawing;

namespace PokemonGoGUI.GoManager.Models
{
    public class Log
    {
        public LoggerTypes LoggerType { get; private set; }
        public DateTime Date { get; private set; }
        public string Message { get; private set; }
        public string StackTrace { get; private set; }
        public string ExceptionMessage { get; private set; }

        /*
        public string ExceptionMessage
        {
            get
            {
                if(Exception == null)
                {
                    return String.Empty;

                }

                return Exception.Message;
            }
        }*/

        public Log(LoggerTypes type, string message, Exception exception = null)
        {
            this.LoggerType = type;
            this.Message = message;
            this.Date = DateTime.Now;

            if (exception != null)
            {
                this.StackTrace = exception.StackTrace;
                this.ExceptionMessage = exception.Message;
            }
        }

        public Color GetLogColor()
        {
            switch (this.LoggerType)
            {
                case LoggerTypes.FatalError:
                    return Color.Red;
                case LoggerTypes.Exception:
                    return Color.Red;
                case LoggerTypes.Success:
                    return Color.Green;
                case LoggerTypes.Warning:
                    return Color.Yellow;
                case LoggerTypes.ProxyIssue:
                    return Color.Yellow;
                case LoggerTypes.PokemonFlee:
                    return Color.Salmon;
                case LoggerTypes.PokemonEscape:
                    return Color.DarkGoldenrod;
                case LoggerTypes.Transfer:
                    return Color.MediumAquamarine;
                case LoggerTypes.Evolve:
                    return Color.MediumAquamarine;
                case LoggerTypes.Incubate:
                    return Color.MediumAquamarine;
                case LoggerTypes.Recycle:
                    return Color.MediumAquamarine;
                case LoggerTypes.Info:
                    return Color.Teal;
                case LoggerTypes.Debug:
                    return Color.DarkGray;
                case LoggerTypes.LocationUpdate:
                    return Color.LightGreen;
                case LoggerTypes.Gym:
                    return Color.Turquoise;
                case LoggerTypes.Captcha:
                    return Color.Tomato;
                case LoggerTypes.Deploy:
                    return Color.DeepSkyBlue;
                case LoggerTypes.Buddy:
                    return Color.LightGreen;
                case LoggerTypes.AwardedBadges:
                    return Color.Beige;
                case LoggerTypes.HatchedEggs:
                    return Color.Coral;
                case LoggerTypes.Upgrade:
                    return Color.Aqua;
                case LoggerTypes.LevelUp:
                    return Color.LightSeaGreen;
                case LoggerTypes.Snipe:
                    return Color.Cyan;
            }
            return SystemColors.WindowText;
        }

        public override string ToString()
        {
            if (!String.IsNullOrEmpty(ExceptionMessage))
            {
                return String.Format("Date: {0} Type: {1} Message: {2} Exception: {3}", Date, LoggerType, Message, ExceptionMessage);
            }

            return String.Format("Date: {0} Type: {1} Message: {2}", Date, LoggerType, Message);
        }
    }
}
