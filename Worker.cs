using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;

public class IdleTrackingService : ServiceBase
{
    private Timer? _timer;
    private bool _emailSent = false;
    private DateTime _idleStart;

    public IdleTrackingService()
    {
        ServiceName = "IdleTrackingService";
    }

    protected override void OnStart(string[] args)
    {
        Log("Service started.");
        _timer = new Timer(CheckIdleTime, null, 0, 5000); // check every 5 seconds
    }

    private void CheckIdleTime(object state)
    {
        var idleTime = GetIdleTime();

        if (idleTime.TotalMinutes >= 5)
        {
            if (!_emailSent)
            {
                _idleStart = DateTime.Now.AddMinutes(-5);
                SendIdleEmail(idleTime);
                Log($"Idle detected for {idleTime.TotalMinutes:F1} minutes.");
                _emailSent = true;
            }
        }
        else
        {
            if (_emailSent)
                Log("User active again.");
            _emailSent = false;
        }
    }

    protected override void OnStop()
    {
        SendServiceStoppedEmail();
        Log("Service stopped.");
    }

    protected override void OnShutdown()
    {
        SendServiceStoppedEmail();
        Log("Service shutdown.");
    }

    private TimeSpan GetIdleTime()
    {
        var lastInputInfo = new LASTINPUTINFO();
        lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

        if (!GetLastInputInfo(ref lastInputInfo))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        uint idleTicks = ((uint)Environment.TickCount - lastInputInfo.dwTime);
        return TimeSpan.FromMilliseconds(idleTicks);
    }

    private void SendIdleEmail(TimeSpan idleTime)
    {
        string machineName = Environment.MachineName;
        string body = $"Machine: {machineName}\n" +
                      $"Idle Duration: {idleTime.TotalMinutes:F1} minutes\n" +
                      $"Idle Since: {_idleStart}";

        SendEmail("Idle Alert", body);
    }

    private void SendServiceStoppedEmail()
    {
        string machineName = Environment.MachineName;
        string body = $"Machine: {machineName}\nService stopped at {DateTime.Now}.";
        SendEmail("Service Stopped", body);
    }

    private void SendEmail(string subject, string body)
    {
        try
        {
            using (var client = new SmtpClient("smtp.example.com", 587))
            {
                client.Credentials = new NetworkCredential("spectrums123@gmail.com", "password");
                client.EnableSsl = true;
                client.Send("spectrums123@gmail.com", "admin@example.com", subject, body);
            }
            Log($"Email sent: {subject}");
        }
        catch (Exception ex)
        {
            Log($"Email failed: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "idle-log.txt");
        File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
}
