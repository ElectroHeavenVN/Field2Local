using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Field2Local
{
    internal class Program
    {
        public static ModuleDefMD currentModule;

        [DllImport("msvcrt.dll")]
        static extern int system(string cmd);

        static void Main(string[] args)
        {
            Console.InputEncoding = Console.OutputEncoding = Encoding.Unicode;
            if (args.Length == 0)
            {
                Console.Write("File path: ");
                args = new string[] { Console.ReadLine().Replace("\"", "") };
            }
            currentModule = ModuleDefMD.Load(args[0]);
            Dictionary<FieldDef, List<MethodDef>> local2Fields = new Dictionary<FieldDef, List<MethodDef>>();
            foreach (TypeDef type in currentModule.GetTypes().Where(t => t != currentModule.GlobalType && t.DeclaringType != currentModule.GlobalType))
            {
                foreach (MethodDef method in type.Methods.Where(m => m.HasBody && m.Body.HasInstructions))
                {
                    foreach (Instruction instruction in method.Body.Instructions.Where(i => (i.OpCode == OpCodes.Stsfld || i.OpCode == OpCodes.Ldsfld || i.OpCode == OpCodes.Ldsflda) && i.Operand is FieldDef))
                    {
                        if (local2Fields.ContainsKey((FieldDef)instruction.Operand))
                        {
                            if (!local2Fields[(FieldDef)instruction.Operand].Contains(method))
                                local2Fields[(FieldDef)instruction.Operand].Add(method);
                        }
                        else
                            local2Fields.Add((FieldDef)instruction.Operand, new List<MethodDef>() { method });
                    }
                }
            }
            Dictionary<FieldDef, Local> field2Local = new Dictionary<FieldDef, Local>();
            foreach (var item in local2Fields.Where(k => k.Value.Count == 1).Select(k => new KeyValuePair<FieldDef, Local>(k.Key, new Local(k.Key.FieldType))))
                field2Local.Add(item.Key, item.Value);

            foreach (TypeDef type in currentModule.GetTypes().Where(t => t != currentModule.GlobalType && t.DeclaringType != currentModule.GlobalType))
            {
                foreach (MethodDef method in type.Methods.Where(m => m.HasBody && m.Body.HasInstructions))
                {
                    foreach (Instruction instruction in method.Body.Instructions.Where(i => (i.OpCode == OpCodes.Stsfld || i.OpCode == OpCodes.Ldsfld || i.OpCode == OpCodes.Ldsflda) && i.Operand is FieldDef))
                    {
                        if (!field2Local.ContainsKey((FieldDef)instruction.Operand))
                            continue;
                        Local local = field2Local[(FieldDef)instruction.Operand];
                        if (!method.Body.Variables.Contains(local))
                            method.Body.Variables.Add(local);
                        instruction.Operand = local;
                        if (instruction.OpCode == OpCodes.Ldsfld)
                            instruction.OpCode = OpCodes.Ldloc;
                        if (instruction.OpCode == OpCodes.Ldsflda)
                            instruction.OpCode = OpCodes.Ldloca;
                        if (instruction.OpCode == OpCodes.Stsfld)
                            instruction.OpCode = OpCodes.Stloc;
                    }
                    method.Body.OptimizeBranches();
                    method.Body.OptimizeMacros();
                }
            }

            ModuleWriterOptions moduleWriterOptions = new ModuleWriterOptions(currentModule);
            string path = Path.GetDirectoryName(args[0]) + "\\" + Path.GetFileNameWithoutExtension(args[0]) + "-Field2Local" + Path.GetExtension(args[0]);
            moduleWriterOptions.MetadataLogger = new Logger();
            try
            {
                currentModule.Write(path, moduleWriterOptions);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogException(ex);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                moduleWriterOptions.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
                currentModule.Write(path, moduleWriterOptions);
            }
            system("pause");
        }
    }
}
