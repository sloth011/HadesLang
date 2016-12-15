﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Interpreter;
using Variables;

namespace HadesLang
{
    class Program
    {
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            var interpreter = new ShellInterpreter();
            Cache.Instance.Variables = new Dictionary<string,Types>();
            while (true)
            {
                Console.Write(">");
                var res = Console.ReadLine();
                Console.WriteLine(interpreter.InterpretLine(res));
            }
        }
    }
}