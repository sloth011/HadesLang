﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Exceptions;
using NCalc;
using StringExtension;
using Variables;
using InvalidOperationException = Exceptions.InvalidOperationException;

namespace Interpreter
{
    public class Evaluator
    {
        public static string VarPattern = @"as (num|dec|word|binary)+ (reachable|reachable_all|closed)+";

        public static List<string> OperatorList = new List<string>
        {
            "+",
            "-",
            "*",
            "/",
            "Sqrt",
            "Sin",
            "Cos",
            "Tan",
            "Pow",
            "[Pi]"
        };

        public static List<string> CompOperatorList = new List<string> {"is", "or", "and", "not", "smaller", "bigger"};
        public bool ForceOut = false;

        public EvaluatedOperation EvaluateBool(string toEvaluate, string access)
        {
            toEvaluate = ReplaceWithVars(toEvaluate, access);
            var reg = Regex.Match(toEvaluate.Replace(" ", ""), @"\[([^]]*)\]").Groups[1].Value;
            var func =
                EvaluateOperation(
                    toEvaluate.Replace(Regex.Match(toEvaluate.Replace(" ", ""), @"\[([^]]*)\]").Groups[0].Value, ""),
                    toEvaluate);

            if (!string.IsNullOrWhiteSpace(reg))
            {
                if (reg == "true" || reg == "false")
                {
                    return new EvaluatedOperation(func, bool.Parse(reg));
                }

                if (reg.ContainsFromList(CompOperatorList))
                {
                    reg = reg.Replace("smallerIs", "<=");
                    reg = reg.Replace("biggerIs", ">=");
                    reg = reg.Replace("xor", "^");
                    reg = reg.Replace("is", "==");
                    reg = reg.Replace("or", "||");
                    reg = reg.Replace("and", "&&");
                    reg = reg.Replace("not", "!=");
                    reg = reg.Replace("smaller", "<");
                    reg = reg.Replace("bigger", ">");

                    var e = new Expression(reg).Evaluate().ToString().ToLower();

                    if (e.IsNum())
                    {
                        if (e == "1")
                        {
                            e = "true";
                        }
                        if (e == "0")
                        {
                            e = "false";
                        }
                    }

                    return new EvaluatedOperation(func, bool.Parse(e));
                }
            }
            return null;
        }

        public bool TryEvaluateBool(string toEvaluate, string access, out string result)
        {
            try
            {
                toEvaluate = ReplaceWithVars(toEvaluate, access);
                var reg = Regex.Match(toEvaluate.Replace(" ", ""), @"\[([^]]*)\]").Groups[1].Value;

                if (reg == "")
                {
                    reg = toEvaluate;
                }

                if (!string.IsNullOrWhiteSpace(reg))
                {
                    if (reg == "true" || reg == "false")
                    {
                        result = reg;
                        return true;
                    }

                    if (reg.ContainsFromList(CompOperatorList))
                    {
                        reg = reg.Replace("smallerIs", "<=");
                        reg = reg.Replace("biggerIs", ">=");
                        reg = reg.Replace("is", "==");
                        reg = reg.Replace("xor", "^");
                        reg = reg.Replace("or", "||");
                        reg = reg.Replace("and", "&&");
                        reg = reg.Replace("not", "!=");
                        reg = reg.Replace("smaller", "<");
                        reg = reg.Replace("bigger", ">");

                        var e = new Expression(reg).Evaluate().ToString().ToLower();

                        if (e.IsNum())
                        {
                            if (e == "1")
                            {
                                e = "true";
                            }
                            if (e == "0")
                            {
                                e = "false";
                            }
                        }

                        result = e;
                        return true;
                    }
                }
            }
            catch (Exception)
            {
            }
            result = null;
            return false;
        }

        public string CreateVariable(string toEvaluate, string access)
        {
            try
            {
                var data = toEvaluate.Split(' ').ToList();
                data.RemoveAll(s => s.Equals("") || s.Equals("="));
                var dt = TypeParser.ParseDataType(data[2]);
                var at = TypeParser.ParseAccessType(data[3]);

                if (Exists(new Tuple<string, string>(data[0], access)))
                {
                    ForceOut = true;
                    throw new DefinationDeniedException("Variable has already been defined with acces type: reachable_all");
                }
                if (data.Count > 4)
                {
                    for (int i = 5; i < data.Count; i++)
                    {
                        data[4] += $" {data[i]}";
                    }

                    Cache.Instance.Variables.Add(new Tuple<string, string>(data[0], access), new Types(at, dt, ""));
                    return AssignValueToVariable(data[0] + "=" + data[4], access);
                }
                else
                {
                    Cache.Instance.Variables.Add(new Tuple<string, string>(data[0], access), new Types(at, dt, ""));
                    return $"{data[0]} is undefined";
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public string AssignValueToVariable(string toEvaluate, string access)
        {
            try
            {
                var data = toEvaluate.Split('=');
                var index = data[0].Replace(" ", "");
                var dt = GetVariable(index, access).Value.DataType;
                data[1] = ReplaceWithVars(data[1], access);
                var isOut = false;

                if (data[1].Contains(":"))
                {
                    var operation = data[1].Split(':');
                    operation[0] = operation[0].Replace(" ", "");
                    data[1] = "'" + EvaluateCall(operation, access) + "'";
                    isOut = true;
                }

                if (data[1].ContainsFromList(OperatorList))
                {
                    data[1] = EvaluateCalculation(data[1]);
                }

                if (Regex.IsMatch(data[1], @"\[([^]]*)\]"))
                {
                    data[1] = EvaluateBool(data[1], access).Result.ToString().ToLower();
                }

                if (dt == DataTypeFromData(data[1]) || isOut || (dt == DataTypes.DEC && DataTypeFromData(data[1]) == DataTypes.NUM))
                {
                    if (dt == DataTypes.WORD)
                    {
                        data[1] = Regex.Match(data[1], @"\'([^]]*)\'").Groups[1].Value;
                    }

                    if (dt == DataTypes.DEC)
                    {
                        data[1] = data[1].Replace(",", ".");
                    }
                    SetVariable(index, data[1], access);
                }
                else
                {
                    throw new InvalidDataAssignException(
                        "The data type of the variable does not match the assignment type!");
                }

                return $"{index} is {data[1]}";
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public KeyValuePair<string, Types> GetVariable(string index, string access)
        {
            if (Exists(new Tuple<string, string>(index, access)))
            {
                foreach (var varN in Cache.Instance.Variables)
                {
                    if (varN.Value.Access == AccessTypes.REACHABLE_ALL && varN.Key.Item1 == index)
                    {
                        return new KeyValuePair<string, Types>(varN.Key.Item1,varN.Value);
                    }
                }

                return new KeyValuePair<string, Types>(index, Cache.Instance.Variables[new Tuple<string, string>(index,access)]);
            }
            else
            {
                throw new AccessDeniedException("You are note allowed to access this variable!");
            }
        }

        public void SetVariable(string variable, string value, string access)
        {
            foreach (var varN in Cache.Instance.Variables)
            {
                if (varN.Value.Access == AccessTypes.REACHABLE_ALL && varN.Key.Item1 == variable)
                {
                    Cache.Instance.Variables[varN.Key].Value = value;
                }
            }

            if (Exists(new Tuple<string, string>(variable, access)))
            {
                Cache.Instance.Variables[new Tuple<string, string>(variable,access)].Value = value;
            }
            else
            {
                throw new AccessDeniedException("You are note allowed to access this variable!");
            }
        }

        public string EvaluateCalculation(string toEvaluate)
        {
            toEvaluate = ReplaceWithVars(toEvaluate, "console");
            try
            {
                var data = toEvaluate.Split('+');

                foreach (var variable in data)
                {
                    if (DataTypeFromData(variable) != DataTypes.WORD)
                    {
                        goto tryNumeric;
                    }
                }

                string resultString = string.Empty;
                foreach (var variable in data)
                {
                    resultString += Regex.Match(variable, @"\'([^]]*)\'").Groups[1].Value;
                }

                return resultString;
            }
            catch (Exception)
            {
            }

            tryNumeric:
            try
            {
                var e = new Expression(toEvaluate)
                {
                    Parameters =
                    {
                        ["Pi"] = Math.PI,
                        ["E"] = Math.E
                    }
                };
                return e.Evaluate().ToString().Replace(",", ".");
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public string EvaluateOut(string toEvaluate, bool ignoreQuote, string access)
        {
            if (ignoreQuote)
            {
                toEvaluate = $"'{toEvaluate}'";
            }
            try
            {
                if (Regex.IsMatch(toEvaluate, @"\[([^]]*)\]"))
                {
                    return EvaluateBool(toEvaluate, access).Result.ToString().ToLower();
                }
                if (toEvaluate.ContainsFromList(OperatorList))
                {
                    return EvaluateCalculation(toEvaluate);
                }
                if (Regex.IsMatch(toEvaluate, @"\'([^]]*)\'"))
                {
                    return Regex.Match(toEvaluate, @"\'([^]]*)\'").Groups[1].Value;
                }
                if (Cache.Instance.Variables.ContainsKey(new Tuple<string, string>(toEvaluate,access)))
                {
                    return GetVariable(toEvaluate, access).Value.Value;
                }
                else
                {
                    throw new VariableNotDefinedException("Variable not defined!");
                }
            }
            catch (VariableNotDefinedException ve)
            {
                return ve.Message;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public KeyValuePair<string, bool> EvaluateCall(string[] toEvaluate, string access)
        {
            if (toEvaluate.Length == 1)
            {
                string tryResult;
                if (TryEvaluateBool(toEvaluate[0], access, out tryResult))
                {
                    return new KeyValuePair<string, bool>(tryResult, false);
                }
            }

            try
            {
                toEvaluate[1] = ReplaceWithVars(toEvaluate[1], access);
                bool isRec = false;

                if (Regex.IsMatch(toEvaluate[1], @"(\[)([^]]*)(\])"))
                {
                    int index = toEvaluate[1].IndexOf("[");
                    toEvaluate[1] = (index < 0)
                        ? toEvaluate[1]
                        : toEvaluate[1].Remove(index, "[".Length);
                    toEvaluate[1] = toEvaluate[1].Substring(0, toEvaluate[1].LastIndexOf("]"));

                    var result = EvaluateCall(toEvaluate[1].Split(new[] {':'}, 2), access);
                    toEvaluate[1] = result.Key;
                    isRec = true;
                }

                if (toEvaluate[1].Contains("->"))
                {
                    var call = toEvaluate[1].Split(new[] {"->"}, 2, StringSplitOptions.None);

                    if (Cache.Instance.Variables.ContainsKey(new Tuple<string, string>(call[0],access)))
                    {
                        if (GetVariable(call[0], access).Value.DataType == DataTypes.OBJECT)
                        {
                            //Todo call method
                        }
                        else
                        {
                            throw new InvalidDataTypeException("Variable is not an object!");
                        }
                    }
                    else
                    {
                        throw new VariableNotDefinedException("Variable not defined!");
                    }
                }

                string callResult = null;
                switch (toEvaluate[0])
                {
                    case "out":
                        callResult = EvaluateOut(toEvaluate[1], isRec, access);
                        ForceOut = true;
                        goto returnResult;
                    case "load":
                        callResult = LoadFile(toEvaluate[1],access);
                        goto returnResult;
                    case "type":
                        callResult = GetVarType(toEvaluate[1], access);
                        goto returnResult;
                    case "dtype":
                        callResult = DataTypeFromData(toEvaluate[1]).ToString().ToLower();
                        goto returnResult;
                    case "uload":
                        callResult = DeleteVar(toEvaluate[1], access);
                        goto returnResult;
                    case "dumpVars":
                        callResult = DumpAllVariables(toEvaluate[1]);
                        goto returnResult;
                    case "rand":
                        callResult = GetRandom(toEvaluate[1]);
                        goto returnResult;
                    case "exists":
                        return
                            new KeyValuePair<string, bool>(Exists(new Tuple<string, string>(toEvaluate[1], access)).ToString().ToLower(), false);
                    case "exit":
                        try
                        {
                            Environment.Exit(int.Parse(toEvaluate[1]));
                        }
                        catch (Exception e)
                        {
                            return new KeyValuePair<string, bool>(e.Message, true);
                        }
                        break;
                }

                returnResult:
                if (ForceOut)
                {
                    ForceOut = false;
                    return new KeyValuePair<string, bool>(callResult, true);
                }
                return new KeyValuePair<string, bool>(callResult, false);
            }
            catch (Exception e)
            {
                return new KeyValuePair<string, bool>(e.Message, true);
            }

            return new KeyValuePair<string, bool>();
        }

        private string LoadFile(string s,string access)
        {
            try
            {
                if (Regex.IsMatch(s, @"'([^]]*)' (as)+"))
                {
                    if (!Regex.IsMatch(s, @"'([^]]*)'"))
                    {
                        throw new InvalidFileNameException("Filename is invalid!");
                    }
                    var fiW = new FileInterpreter(s);
                    fiW.LoadFunctions();
                    fiW.LoadReachableVars();

                    var kwp = new KeyValuePair<Tuple<string,string>,Types>(new Tuple<string, string>(Regex.Split(s, @"(as)")[1].Replace(" ", ""),access), new Types(AccessTypes.CLOSED, DataTypes.OBJECT, ""));
                    kwp.Value.Lines = fiW.Lines;
                    kwp.Value.Methods = fiW.Methods;

                    Cache.Instance.Variables.Add(kwp.Key,kwp.Value);
                }
                if (Regex.IsMatch(s, @"'([^]]*)'"))
                {
                    var fiL = new FileInterpreter(s);
                    fiL.LoadAll();
                    return "";
                }
                throw new InvalidFileNameException("Filename is invalid!");
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        private string GetRandom(string s)
        {
            if (s == "void")
            {
                return new Random().Next().ToString();
            }
            else
            {
                try
                {
                    return new Random().Next(int.Parse(s)).ToString();
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }
        }

        private string DumpAllVariables(string s)
        {
            var sb = new StringBuilder();
            DataTypes dt = DataTypes.NONE;

            if (s != "all")
            {
                try
                {
                    dt = DataTypeFromString(s.ToUpper());
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }

            foreach (var variable in Cache.Instance.Variables)
            {
                if (variable.Value.DataType == dt || dt == DataTypes.NONE)
                {
                    sb.Append($"{variable.Key.Item1}@{variable.Key.Item2} = {variable.Value.Value}\n");
                }
            }

            sb.Length = sb.Length - 1;

            return sb.ToString();
        }

        private string DeleteVar(string s,string access)
        {
            try
            {
                if (Cache.Instance.Variables.ContainsKey(new Tuple<string, string>(s,access)))
                {
                    RemoveVariable(s,access);
                    return "Variable unloaded!";
                }
                else
                {
                    throw new VariableNotDefinedException("Variable not defined!");
                }
            }
            catch (VariableNotDefinedException ve)
            {
                return ve.Message;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        private void RemoveVariable(string s, string access)
        {
            if (Exists(new Tuple<string, string>(s,access)))
            {
                Cache.Instance.Variables.Remove(new Tuple<string, string>(s,access));
            }
            else
            {
                throw new AccessDeniedException("You are note allowed to access this variable!");
            }
        }

        private bool Exists(Tuple<string, string> instanceVariable)
        {
            foreach (var variable in Cache.Instance.Variables)
            {
                if (variable.Value.Access == AccessTypes.REACHABLE_ALL && variable.Key.Item1 == instanceVariable.Item1)
                {
                    return true;
                }
            }

            try
            {
                var test = Cache.Instance.Variables[instanceVariable].Value;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public string ReplaceWithVars(string s,string access)
        {
            var reg = new Regex(@"\{([^\}]+)\}");
            var matches  = reg.Matches(s);

            foreach (var variable in matches)
            {
                var varName = variable.ToString().Replace("{", "").Replace("}", "");
                var data = GetVariable(varName, access).Value.Value;

                if (GetVariable(varName, access).Value.DataType == DataTypes.WORD)
                {
                    s = s.Replace(variable.ToString(), $"'{data}'");
                }
                else
                {
                    s = s.Replace(variable.ToString(), data);
                }

            }

            return s;
        }

        private string GetVarType(string s,string access)
        { 
            try
            {
                if (Cache.Instance.Variables.ContainsKey(new Tuple<string, string>(s,access)))
                {
                    return GetVariable(s, access).Value.DataType.ToString().ToLower();
                }
                else
                {
                    throw new VariableNotDefinedException("Variable not defined!");
                }
            }
            catch (VariableNotDefinedException ve)
            {
                return ve.Message;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public DataTypes DataTypeFromData(string data)
        {
            if (data.IsNum())
            {
                return DataTypes.NUM;
            }

            if (data.IsBinary())
            {
                return DataTypes.BINARY;
            }

            if (data.IsDec())
            {
                return DataTypes.DEC;
            }

            if (Regex.IsMatch(data, @"\'([^]]*)\'"))
            {
                return DataTypes.WORD;
            }

            throw new InvalidDataTypeException("Invalid data type!");
        }

        public DataTypes DataTypeFromString(string typeName)
        {
            switch (typeName.ToLower())
            {
                case "word":
                    return DataTypes.WORD;
                case "num":
                    return DataTypes.NUM;
                case "dec":
                    return DataTypes.DEC;
                case "binary":
                    return DataTypes.BINARY;
                default:
                    throw new InvalidDataTypeException("Given data type was invalid!");
            }
        }

        public OperationTypes EvaluateOperation(string operation, string toEvaluate)
        {
            if (operation == toEvaluate || operation == "")
            {
                return OperationTypes.NONE;
            }

            switch (operation)
            {
                case "case":
                    return OperationTypes.CASE;
                case "runala":
                    return OperationTypes.RUNALA;
                default:
                    throw new InvalidOperationException("Invalid operation!");
            }
        }
    }
}
