using System;
using System.Configuration;
using NUnit.Framework;
using System.Reflection;
using System.IO;

namespace Jarvis.MonitoringAgent.Tests
{
    [SetUpFixture]
    public class GlobalSetup
    {
        [OneTimeSetUp]
        public void Global_initialization_of_all_tests()
        {
            Console.WriteLine($"Environment current path is: {Environment.CurrentDirectory}");
            //Nunit3 fix for test adapter of visual studio, it uses visual studio test directory
            Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }
    }
}

