﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StringExtension
{
    public static class Extensions
    {
        public static bool IsBinary(this string s)
        {
            bool x;
            return bool.TryParse(s,out x);
        }

        public static bool IsNum(this string s)
        {
            int x;
            return int.TryParse(s, out x);
        }

        public static bool IsDec(this string s)
        {
            double x;
            return double.TryParse(s, out x);
        }

        public static bool ContainsFromList(this string s, List<string> source)
        {
            foreach (var variable in source)
            {
                if (s.Contains(variable))
                {
                    return true;
                }
            }
            return false;
        }
    }
}