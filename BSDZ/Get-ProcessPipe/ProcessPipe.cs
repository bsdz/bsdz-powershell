/*
Copyright (c) 2008 Blair Sutton. All rights reserved.
For more information please see http://bsdz-ramblings.blogspot.com/2008/08/powershell-performance.html
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Management.Automation;
using System.Collections;
using System.Diagnostics;
using System.Threading;

namespace BSDZ
{

  [Cmdlet(VerbsCommon.Get, "ProcessPipe", SupportsShouldProcess = true)]
  public class ProcessPipe : Cmdlet
  {

    #region Parameters
    [Parameter(Position = 0,
        Mandatory = false,
        ValueFromPipelineByPropertyName = true,
        ValueFromPipeline = true,
        HelpMessage = "Pipeline input.")]
    [ValidateNotNullOrEmpty]
    public string[] Name
    {
      get { return name; }
      set { name = value; }
    }
    private string[] name;

    [Parameter(Position = 1,
        Mandatory = true,
        HelpMessage = "Process path")]
    [ValidateNotNullOrEmpty]
    public string ProcessPath
    {
      get { return processpath; }
      set { processpath = value; }
    }
    private string processpath;

    [Parameter(Position = 2,
        Mandatory = true,
        HelpMessage = "Process arguments")]
    [ValidateNotNullOrEmpty]
    public string Arguments
    {
      get { return arguments; }
      set { arguments = value; }
    }
    private string arguments;

    [Parameter(Position = 3,
        Mandatory = false,
        HelpMessage = "Record separator")]
    public string Separator
    {
      get { return separator; }
      set { separator = value; }
    }
    private string separator = "\r\n";
    #endregion

    #region Properties
    private Process process;
    private Process Process
    {
      get { return process; }
      set { process = value; }
    }

    private Thread stdoutThread;
    public Thread StdoutThread
    {
      get { return stdoutThread; }
      set { stdoutThread = value; }
    }

    private Queue<String> objectQueue;
    private Queue<String> ObjectQueue
    {
      get { lock (this) { return objectQueue; } }
      set { lock (this) { objectQueue = value; } }
    }

    private int objectsProcessedCount = 0;
    private int ObjectsProcessedCount
    {
      get { return objectsProcessedCount; }
      set { objectsProcessedCount = value; }
    }
    #endregion

    private void ProcessStdout(Process p, Queue<string> oq)
    {
      try
      {
        List<char> dataQueue = new List<char>();
        char[] separatorCharArray = Separator.ToCharArray();
        char[] buffer = new char[16 * 1024];
        int totalBytesRead = 0;
        int totalObjectsQueued = 0;
        int bytesRead;
        while ((bytesRead = p.StandardOutput.Read(buffer, 0, buffer.Length)) > 0)
        {
          totalBytesRead += bytesRead;

          for (int i = 0; i <= bytesRead - 1; i++)
            dataQueue.Add(buffer[i]);

          // pump out any complete objects
          bool completeMatch = true;
          int index;
          while ((index = dataQueue.IndexOf(separatorCharArray[0])) > 0)
          {
            // skip if not enough chars to complete match
            if (dataQueue.Count < index + separatorCharArray.Length)
              continue;

            // check it's a complete match, add if it is
            for (int i = 1; i <= separatorCharArray.Length - 1; i++)
              completeMatch &= dataQueue[index + i] == separatorCharArray[i];

            if (completeMatch)
            {
              oq.Enqueue(new string(dataQueue.GetRange(0, index).ToArray()));
              totalObjectsQueued++;
              dataQueue.RemoveRange(0, index + separatorCharArray.Length);
            }
          }
        }
        
        // enqueue any remaining chars
        if (dataQueue.Count > 0)
        {
          oq.Enqueue(new string(dataQueue.ToArray()));
          dataQueue.Clear();
        }
      }
      catch (Exception e)
      {
        ThrowTerminatingError(new ErrorRecord(e, "Stdout thread threw exception", ErrorCategory.OpenError, null));
      }
    }

    protected override void BeginProcessing()
    {
      base.BeginProcessing();

      try
      {
        ObjectQueue = new Queue<string>();

        ProcessStartInfo si = new ProcessStartInfo();
        si.FileName = ProcessPath;
        si.Arguments = Arguments;
        si.UseShellExecute = false;
        si.RedirectStandardOutput = true;
        si.RedirectStandardInput = true;

        Process = Process.Start(si);

        stdoutThread = new Thread(delegate() { ProcessStdout(Process, ObjectQueue); });
        stdoutThread.Start();
      }
      catch (Exception e)
      {
        ThrowTerminatingError(new ErrorRecord(e, "Failed to prepare process pipeline", ErrorCategory.OpenError, null));
      }
    }

    void DequeueObjects()
    {
      lock (ObjectQueue)
      {
        while (ObjectQueue.Count != 0)
        {
          WriteObject(ObjectQueue.Dequeue());
          ObjectsProcessedCount++;
        }
      }
    }

    int totalBytesIn = 0;
    int totalObjectsIn = 0;
    protected override void ProcessRecord()
    {
      try
      {
        process.StandardInput.WriteLine(Name.GetValue(0));
        totalObjectsIn++;
        process.StandardInput.Flush();
        totalBytesIn += Name.GetValue(0).ToString().Length;

        DequeueObjects();
      }
      catch (Exception e)
      {
        ThrowTerminatingError(new ErrorRecord(e, "Failed to pass pipeline to process", ErrorCategory.ReadError, null));
      }
    }

    protected override void EndProcessing()
    {
      base.EndProcessing();

      try
      {
        process.StandardInput.Close();
        process.WaitForExit();

        stdoutThread.Join();

        DequeueObjects();
      }
      catch (Exception e)
      {
        ThrowTerminatingError(new ErrorRecord(e, "Error in process cleanup", ErrorCategory.CloseError, null));
      }
    }
  }
}
