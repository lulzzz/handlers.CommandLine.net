﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synapse.Handlers.CommandLine
{
    public enum TimeoutActionType
    {
        Continue,
        Error,
        KillProcessAndContinue,
        KillProcessAndError
    }
}
