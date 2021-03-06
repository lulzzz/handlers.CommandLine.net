﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Synapse.Handlers.CommandLine
{
    class LogTailer
    {
        public String FileName { get; set; }
        public Action<string, string> Callback { get; set; }
        public String CallbackLabel { get; set; }
        public int PollingIntervalMills { get; set; }
        public String Contents { get { return stdout.ToString(); } }
        public bool SaveOutput { get; set; } = true;

        bool stop = false;
        Thread thread = null;
        StreamReader reader = null;
        StringBuilder stdout = new StringBuilder();

        public LogTailer() { PollingIntervalMills = 1000; }

        public LogTailer(String server, string fileName, Action<string, string> callback = null, String callbackLabel = null, int pollingIntervalMills = 1000, bool saveOutput = true)
        {
            FileName = "\\\\" + server + "\\" + fileName.Replace(':', '$');

            if (callback != null)
                Callback = callback;
            else
                Callback = LogTailer.ConsoleWriter;

            CallbackLabel = callbackLabel;
            PollingIntervalMills = pollingIntervalMills;
            SaveOutput = saveOutput;
        }

        public LogTailer(String fileName, Action<string, string> callback = null, String callbackLabel = null, int pollingIntervalMills = 1000, bool saveOutput = true)
        {
            FileName = fileName;

            if (callback != null)
                Callback = callback;
            else
                Callback = LogTailer.ConsoleWriter;

            CallbackLabel = callbackLabel;
            PollingIntervalMills = pollingIntervalMills;
            SaveOutput = saveOutput;
        }

        public void Start()
        {
            thread = new Thread(this.TailLog);
            thread.Start();
        }

        public void Stop(double timeoutSeconds = 0, bool deleteFile = false)
        {
            stop = true;
            
            Stopwatch clock = new Stopwatch();
            if (timeoutSeconds > 0)
                clock.Start();

            while (thread.IsAlive)
            {
                if (clock.ElapsedSeconds() > timeoutSeconds)
                {
                    thread.Abort();
                    Callback?.Invoke(CallbackLabel, "LogTailer Thread Did Not Stop In " + timeoutSeconds + " Seconds.  Thread Aborted.");
                    reader.Close();
                    reader.Dispose();
                    Thread.Sleep(PollingIntervalMills);
                }
                else
                    Thread.Sleep(PollingIntervalMills);
            }

            if (timeoutSeconds > 0)
                clock.Stop();

            if (deleteFile)
                DeleteFile();
        }

        public void DeleteFile()
        {
            try
            {
                File.Delete(FileName);
            }
            catch (Exception e)
            {
                Callback?.Invoke(CallbackLabel, e.Message);
                Callback?.Invoke(CallbackLabel, "Unable To Delete File [" + FileName + "]");
            }
        }

        void TailLog()
        {
            if (FileName != null)
            {
                try { reader = new StreamReader(new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)); }
                catch { reader = null; }
            }
            long lastMaxOffset = 0;
            String line = String.Empty;

            while (!stop)
            {
                if (reader != null)
                {
                    if (reader.BaseStream.Length != lastMaxOffset)
                    {
                        //seek to the last max offset
                        reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

                        int i = 0;
                        //read out of the file until the EOF
                        while ((i = reader.Read()) >= 0)
                        {
                            char ch = (char)i;
                            if (ch == '\r' || ch == '\n')
                            {
                                if (SaveOutput)
                                    stdout.AppendLine(line);
                                Callback?.Invoke(CallbackLabel, line);

                                line = String.Empty;

                                // Check To See If Next Character Is Also A LF or CR and Burn It If It Is.
                                char nextCh = (char)reader.Peek();
                                if (nextCh == '\r' || nextCh == '\n')
                                    reader.Read();
                            }
                            else
                                line += ch;
                        }

                        //update the last max offset
                        lastMaxOffset = reader.BaseStream.Position;

                    }
                }
                else
                {
                    if (FileName != null)
                    {
                        try { reader = new StreamReader(new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)); }
                        catch { reader = null; }
                    }
                }

                Thread.Sleep(PollingIntervalMills);
            } // End While

            if (reader == null && FileName != null && stop == true)
            {
                // Stop Command Recieved, But Still Haven't Been Able To Get The Output File.  
                // Pause A Bit And Give It One Last Try.
                Thread.Sleep(PollingIntervalMills * 5);
                try { reader = new StreamReader(new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)); }
                catch
                {
                    reader = null;
                    Callback?.Invoke(CallbackLabel, "Process Completed But Unable To Find Output File [" + FileName + "].");
                }
            }

            // Perform One Last Read Then Close the Reader
            if (reader != null)
            {
                if (reader.BaseStream.Length != lastMaxOffset)
                {
                    //seek to the last max offset
                    reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

                    int i = 0;
                    //read out of the file until the EOF
                    while ((i = reader.Read()) >= 0)
                    {
                        char ch = (char)i;
                        if (ch == '\r' || ch == '\n')
                        {
                            if (SaveOutput)
                                stdout.AppendLine(line);
                            Callback?.Invoke(CallbackLabel, line);

                            line = String.Empty;

                            // Check To See If Next Character Is Also A LF or CR and Burn It If It Is.
                            char nextCh = (char)reader.Peek();
                            if (nextCh == '\r' || nextCh == '\n')
                                reader.Read();
                        }
                        else
                            line += ch;
                    }

                    //update the last max offset
                    lastMaxOffset = reader.BaseStream.Position;

                }

                reader.Close();
                reader.Dispose();
            }
        }

        public static void ConsoleWriter(String label, String message)
        {
            if (String.IsNullOrWhiteSpace(label))
                Console.WriteLine(message);
            else
                Console.WriteLine(label + " : " + message);
        }
    }
}
