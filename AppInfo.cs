using System;
using System.Reflection;

namespace EZ2Play.App
{
    public static class AppInfo
    {
        public static string GetProductName()
        {
            return "EZ2Play";
        }

        public static string GetProductDescription()
        {
            return "EZ2Play is a simple WPF application without any settings that allows you to launch games and applications from base shortcuts.";
        }

        public static string GetProductSlogan()
        {
            return "Simple way to game";
        }

        public static string GetVersion(bool shortFormat = false)
        {
            return "1.3.2.0";
        }

        public static string GetCompanyName()
        {
            return "FreeGen";
        }
    }
}

