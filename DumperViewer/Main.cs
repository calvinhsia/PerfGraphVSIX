﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StressTestUtility;
namespace DumperViewer
{
    class MainProgram
    {
        [STAThread]
        public static void Main(string[] args)
        {
            DumperViewerMain.Main(args);
        }
    }
}