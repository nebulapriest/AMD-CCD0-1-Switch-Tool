using System;
class StartupInstallTests {
 static int Main(){try{StartupTask.ValidateInstall(System.Reflection.Assembly.GetExecutingAssembly().Location);throw new Exception("User-writable workspace unexpectedly accepted for highest-privilege autostart.");}catch(InvalidOperationException ex){if(!ex.Message.Contains("Program Files"))throw;Console.WriteLine("PASS unsafe workspace path rejected for elevated autostart");return 0;}}
}
