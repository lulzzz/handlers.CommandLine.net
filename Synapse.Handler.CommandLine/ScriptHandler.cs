﻿using System;
using System.Xml;
using System.IO;

using Synapse.Core;
using Synapse.Handlers.CommandLine;

public class ScriptHandler : HandlerRuntimeBase
{
    ScriptHandlerConfig config = null;
    String parameters = null;

    public override IHandlerRuntime Initialize(string configStr)
    {
        config = HandlerUtils.Deserialize<ScriptHandlerConfig>(configStr);
        return base.Initialize(configStr);
    }

    override public ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        ExecuteResult result = null;
        parameters = startInfo.Parameters;
        String script = null;

        try
        {
            String command = null;
            String args = null;

            switch (config.Type)
            {
                case ScriptType.Powershell:
                    command = "powershell.exe";
                    if (config.ParameterType == ParameterTypeType.Script)
                    {
                        script = FileUtils.GetTempFileUNC(config.RunOn, config.WorkingDirectory, "ps1");
                        if (script == null)
                            script = FileUtils.GetTempFileUNC(config.RunOn, Path.GetTempPath(), "ps1");
                        File.WriteAllText(script, parameters);
                    }
                    else
                        script = parameters;

                    args = config.Args + @" -File """ + script + @"""";
                    if (!String.IsNullOrWhiteSpace(config.ScriptArgs))
                        args += " " + config.ScriptArgs;
                    break;
                default:
                    throw new Exception("Unknown ScriptType [" + config.Type.ToString() + "] Received.");
            }

            if (String.IsNullOrEmpty(config.RunOn))
                result = LocalProcess.RunCommand(command, args, config.WorkingDirectory, config.TimeoutMills, config.TimeoutAction, SynapseLogger, null, startInfo.IsDryRun);
            else
                result = WMIUtil.RunCommand(command, args, config.RunOn, config.WorkingDirectory, config.TimeoutMills, config.TimeoutAction, SynapseLogger, config.RunOn, startInfo.IsDryRun);

            result.Status = HandlerUtils.GetStatusType(int.Parse(result.ExitData.ToString()), config.ValidExitCodes);

            if (File.Exists(script) && config.ParameterType == ParameterTypeType.Script)
                File.Delete(script);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
            if (File.Exists(script))
                File.Delete(script);
            throw e;
        }

        OnLogMessage(config.RunOn, "Command " + result.Status + " with Exit Code = " + result.ExitData);
        return result;
    }

    public void SynapseLogger(String label, String message)
    {
        OnLogMessage(label, message);
    }

}