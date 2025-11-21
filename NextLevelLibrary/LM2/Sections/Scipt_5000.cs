using AvaloniaToolbox.Core.IO;
using NextLevelLibrary;
using NextLevelLibrary.LM2.Sections;
using System;
using System.Text;
using static FileConverter.LM2.ScriptFormat;
using static NextLevelLibrary.LM2.Sections.ScriptNew;

namespace FileConverter.LM2
{
    public class ScriptFormat
    {
        public class Script
        {
            public HashString Hash;
            public uint StringBufferSize;

            public List<Function> Functions = new List<Function>();
        }

        public class StringHash
        {
            public HashString Hash;
            public HashString Type;
            public uint Offset;
        }

        public class Function
        {
            public HashString Hash;

            public uint CodeStartIndex;
            public uint Flag;

            public List<ScriptInstruction> Instructions = new();

            private Stack<object> _stack = new Stack<object>();

            private Dictionary<int, object> _globals = new(); // global variables
            private Dictionary<int, object> _locals = new();  // local variables

            public void Execute(ScriptFormat script)
            {
                int ip = 0; // instruction pointer

                while (ip < Instructions.Count)
                {
                    var inst = Instructions[ip];
                    switch (inst.OpCode)
                    {
                        case OpCode.LOAD_VAR:
                        case OpCode.LOAD_VAR_L:
                            // Push variable onto stack
                            // Operand is used as an index to the 4 byte data types
                            _stack.Push(script.Data[inst.Operand]);
                            break;
                        case OpCode.LOAD_STR:
                        case OpCode.LOAD_STR_L:
                            // Push string variable onto stack
                            // Operand is used as an offset relative to the string table
                            _stack.Push(script.ReadString((int)inst.Operand));
                            break;
                        case OpCode.PUSH:
                            // Push operand to stack
                            _stack.Push(inst.Operand);
                            break;
                        case OpCode.BRANCH_NZ:
                            // Branch if top of stack is non-zero
                            var v = _stack.Pop();
                            if (v is int vi && vi == 0)
                                ip += (int)inst.Operand;
                            break;
                        case OpCode.JUMP:
                            // Unconditional jump
                            ip += (int)inst.Operand;
                            break;
                        case OpCode.CALL_NATIVE:
                            CallNative(inst.Operand);
                            break;
                        case OpCode.CALL: // Call script
                        case OpCode.CALLG: // Call global script
                            CallScriptFunction(inst.Operand);
                            break;
                        case OpCode.RETURN:
                            return;
                        case OpCode.LDL:
                            _stack.Push(_locals.TryGetValue((int)inst.Operand, out var lVal) ? lVal : 0);
                            return;
                        case OpCode.STL:
                            // Store value from stack to local variable
                            _locals[(int)inst.Operand] = _stack.Pop();
                            break;
                        case OpCode.ADDI:
                            // Add immediate value to top of stack
                           // if (_stack.Count > 0)
                            //    _stack.Push(_stack.Pop() + inst.Operand);
                            break;
                        case OpCode.STG:
                            // Store stack value to global
                            _globals[(int)inst.Operand] = _stack.Pop();
                            break;
                    }
                    ip++;
                }
            }

            private void CallNative(uint operand)
            {
                // 824 = SetVector3(A, B, C)
                // 821 = SetVector4(A, B, C, D)
                // 560 = MATRIX4X4()

            }
            private void CallScriptFunction(uint operand)
            {

            }
        }

        public List<Script> Scripts = new List<Script>();
        public List<StringHash> StringHashes = new List<StringHash>();

        public HashString HashType;

        public int[] Data;
        public byte[] StringTable;

        public string[] HashTable = new string[0];

        public ScriptFormat(ChunkFileEntry chunkFile)
        {
            var headerChunk = chunkFile.GetChild(ChunkType.ScriptHeader);
            var functionTables = chunkFile.GetChildList(ChunkType.ScriptFunctionTable);

            var numScripts = headerChunk.Data.Length / 8;

            //Parse headers
            using (var reader = new FileReader(headerChunk.Data, true))
            {
                for (int i = 0; i < numScripts; i++)
                {
                    Script script = new Script();
                    script.Hash = new HashString(reader.ReadUInt32());
                    //0 unless string hash chunk is used
                    //Possibly the buffer size to allocate the string hashes, which place via offset
                    script.StringBufferSize = reader.ReadUInt32();
                    Scripts.Add(script);
                }
            }

            //Parse hashes
            var strHashChunk = chunkFile.GetChild(ChunkType.ScriptStringHashes);
            if (strHashChunk != null)
            {
                int index = 0;
                using (var reader = new FileReader(strHashChunk.Data, true))
                {
                    uint numFuncs = (uint)strHashChunk.Data.Length / 12;
                    for (int j = 0; j < numFuncs; j++)
                    {
                        StringHash strHash = new StringHash();
                        strHash.Hash = new HashString(reader.ReadUInt32());
                        strHash.Type = new HashString(reader.ReadUInt32()); // "ScriptState"
                        strHash.Offset = reader.ReadUInt32(); // Placement in the stack?
                        StringHashes.Add(strHash);
                    }
                }
            }

            //This should not happen
            if (functionTables.Count != Scripts.Count)
                throw new Exception("Unexpected table count!");

            for (int i = 0; i < Scripts.Count; i++)
            {
                using (var reader = new FileReader(functionTables[i].Data, true))
                {
                    uint numFuncs = (uint)functionTables[i].Data.Length / 12;
                    for (int j = 0; j < numFuncs; j++)
                    {
                        Function func = new Function();
                        func.Hash = new HashString(reader.ReadUInt32());
                        func.CodeStartIndex = reader.ReadUInt32();
                        func.Flag = reader.ReadUInt32();
                        Scripts[i].Functions.Add(func);
                    }
                }
            }

            //Optional hash bundles
            var hashBundleChunk = chunkFile.GetChild(ChunkType.ScriptHashBundle);
            if (hashBundleChunk != null)
            {
                using (var reader = new FileReader(hashBundleChunk.Data, true))
                {
                    uint[] hashes = reader.ReadUInt32s((int)hashBundleChunk.Data.Length / 4);
                    HashTable = new string[hashes.Length];
                    for (int i = 0; i < hashes.Length; i++)
                        HashTable[i] = Hashing.GetString(hashes[i]);
                }
            }

            //Raw data
            var dataChunk = chunkFile.GetChild(ChunkType.ScriptData);
            try
            {
                ParseScriptData(new FileReader(dataChunk.Data, true), Scripts);
            }
            catch (Exception ex) 
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public string ReadString(int offset)
        {
            if (offset < 0 || offset >= StringTable.Length) return $"{offset.ToString()}";

            var slice = this.StringTable.AsSpan().Slice((int)offset);
            int length = slice.IndexOf((byte)0);
            if (length < 0)
                length = slice.Length;
            // Convert bytes to string
            return System.Text.Encoding.UTF8.GetString(slice.Slice(0, length));
        }

        public void Save(ChunkFileEntry chunkFile)
        {
            // Write headers
            var headerChunk = chunkFile.GetChild(ChunkType.ScriptHeader);
            headerChunk.Data = new MemoryStream();
            using (var writer = new FileWriter(headerChunk.Data, true))
            {
                foreach (var script in this.Scripts)
                {
                    writer.Write(script.Hash.Value);
                    writer.Write(script.StringBufferSize);
                }
            }
            // Script data
            var dataChunk = chunkFile.GetChild(ChunkType.ScriptData);
            dataChunk.Data = new MemoryStream();
            using (var writer = new FileWriter(dataChunk.Data, true))
                WriteScriptData(writer);
            // Script Functions
            var scriptFunctionChunk = chunkFile.GetChild(ChunkType.ScriptFunctionTable);
            dataChunk.Data = new MemoryStream();
            using (var writer = new FileWriter(dataChunk.Data, true))
            {
                for (int i = 0; i < Scripts.Count; i++)
                {
                    foreach (var func in Scripts[i].Functions)
                    {
                        writer.Write(func.Hash.Value);
                        writer.Write(func.CodeStartIndex);
                        writer.Write(func.Flag);
                    }
                }
            }
            // Script state hashes
            if (StringHashes.Count > 0)
            {
                var strHashChunk = chunkFile.GetChild(ChunkType.ScriptStringHashes);
                dataChunk.Data = new MemoryStream();
                using (var writer = new FileWriter(dataChunk.Data, true))
                {
                    foreach (var v in StringHashes)
                    {
                        writer.Write(v.Hash.Value);
                        writer.Write(v.Type.Value);
                        writer.Write(v.Offset);
                    }
                }
            }
        }

        void ParseScriptData(FileReader reader, List<Script> scripts)
        {
            HashType = new HashString(reader.ReadUInt32()); //FlowScript or some unknown hash
            uint codeSize = reader.ReadUInt32();
            uint dataSize = reader.ReadUInt32();
            ushort stringTableSize = reader.ReadUInt16();
            ushort num = reader.ReadUInt16(); //unk. Usually 0

            var pos = reader.Position;
            Data = reader.ReadInt32s((int)dataSize / 4);
            var code = reader.ReadUInt16s((int)codeSize / 2);
            StringTable = reader.ReadBytes((int)(reader.BaseStream.Length - reader.Position));

            var strPos = 16 + codeSize + dataSize;

            reader.SeekBegin(pos + dataSize);
            long codePos = reader.Position;
            foreach (var script in scripts)
            {
                for (int i = 0; i < script.Functions.Count; i++)
                {
                    var instructions = ReadFunction(reader, script.Functions[i], codePos, strPos, pos, dataSize);
                    script.Functions[i].Instructions.AddRange(instructions);
                }
            }
        }

        private List<ScriptInstruction> ReadFunction(FileReader reader, Function func, long codePos, long strPos, long dataPos, uint dataSize)
        {
            var instructions = new List<ScriptInstruction>();

            reader.SeekBegin(codePos + func.CodeStartIndex * 2);
            while (reader.Position < reader.BaseStream.Length)
            {
                var offset = (int)(reader.Position - codePos);
                ushort code = reader.ReadUInt16();

                int opCode = code >> 10;
                uint operand = code & 0xFFFF03FFu;

                var inst = new ScriptInstruction
                {
                    Offset = offset,
                    Raw = code,
                    OpCode = (ScriptNew.OpCode)opCode,
                    Operand = operand
                };
                instructions.Add(inst);

                switch ((ScriptNew.OpCode)opCode)
                {
                    case ScriptNew.OpCode.RETURN:
                        return instructions;
                    case ScriptNew.OpCode.LOAD_VAR_L: // Load global variable long
                    case ScriptNew.OpCode.LOAD_STR_L: // Load constant offset long
                    case ScriptNew.OpCode.CALL_NATIVE_L: // call native function long
                    case ScriptNew.OpCode.ADDI_L: // Add immediate long
                    case ScriptNew.OpCode.LDGA_L: // Load global address long
                    case ScriptNew.OpCode.JUMP: // Jump (always long value)
                        // Values are extended
                        var low = reader.ReadUInt16();
                        inst.Operand = (inst.Operand << 16) | low;
                        break;
                    case ScriptNew.OpCode.BRANCH_NZ: // Branches when non-zero 
                    case ScriptNew.OpCode.STG: // stores/copies data via index
                        inst.Extra = reader.ReadUInt16();
                        inst.HasExtra = true;
                        break;
                }
            }

            return instructions;
        }

        private void WriteScriptData(FileWriter writer)
        {
            writer.Write(HashType.Value);
            writer.Write(0); // code size
            writer.Write(this.Data.Length * 4); // data size
            writer.Write((ushort)this.StringTable.Length); // string size
            writer.Write((ushort)0);
            writer.Write(this.Data);
            // Code data
            var codeStart = writer.Position;
            foreach (var script in Scripts)
            {
                foreach (var func in script.Functions.OrderBy(x => x.CodeStartIndex))
                {
                    // Keep same order
                    func.CodeStartIndex = (uint)(writer.Position - codeStart) / 2;
                    WriteFunction(writer, func);
                }
            }
            var codeEnd = writer.Position;
            writer.Write(this.StringTable);
            // Code size
            writer.WriteSectionSizeU32(4, codeEnd - codeStart);
        }

        private void WriteFunction(FileWriter writer, Function func)
        {
            foreach (var inst in func.Instructions)
            {
                ushort code = (ushort)(((int)inst.OpCode << 10) | ((int)inst.Operand & 0x3FF));
                writer.Write(code);

                switch (inst.OpCode)
                {
                    case ScriptNew.OpCode.LOAD_VAR_L:
                    case ScriptNew.OpCode.LOAD_STR_L:
                    case ScriptNew.OpCode.CALL_NATIVE_L:
                    case ScriptNew.OpCode.ADDI_L:
                    case ScriptNew.OpCode.LDGA_L:
                    case ScriptNew.OpCode.JUMP:
                        {
                            // Operand is extended by lower 16 bits
                            ushort low = (ushort)(inst.Operand & 0xFFFF);
                            writer.Write(low);
                            break;
                        }
                    case ScriptNew.OpCode.BRANCH_NZ:
                    case ScriptNew.OpCode.STG:
                        {
                            writer.Write((ushort)inst.Extra);
                            break;
                        }
                    default:
                        break;
                }
                if (inst.OpCode == ScriptNew.OpCode.RETURN)
                    break;
            }
        }

        public void Export(string path)
        {
           // File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public string ToString()
        {
            var sb = new StringBuilder();
            foreach (var script in this.Scripts)
            {
                sb.AppendLine($"Script: {script.Hash} StringBuffer: {script.StringBufferSize}");
                foreach (var func in script.Functions)
                {
                    sb.AppendLine($"    Function: {func.Hash} Flag: {func.Flag}");
                    foreach (var inst in func.Instructions)
                    {
                        var desc = ScriptNew.OpCodeDescriptions.TryGetValue(inst.OpCode, out var d) ? d : "";

                        if (inst.OpCode == ScriptNew.OpCode.CALLE)
                        {
                            sb.AppendLine(
                                    $"        {inst.Offset,6:X4}  " +
                                    $"{inst.OpCode,-15} " +
                                    $"{(OperationTableOpCode0xB)inst.Operand,-15} " +
                                    $"{(inst.HasExtra ? inst.Extra.ToString() : ""),8}"
                                );
                        }
                        else
                        {
                            sb.AppendLine(
                                    $"        {inst.Offset,6:X4}  " +
                                    $"{inst.OpCode,-15} " +
                                    $"{inst.Operand,8} " +
                                    $"{(inst.HasExtra ? inst.Extra.ToString() : ""),8}"
                                );
                        }
                    }
                }
            }
            return sb.ToString();
        }

        public struct ScriptInstruction
        {
            public int Offset;
            public ushort Raw;
            public ScriptNew.OpCode OpCode;
            public uint Operand;
            public bool HasExtra;
            public ushort Extra;
        }

        public enum NativeCodeTableOpCode0x7
        {
            VEC3 = 824,
            VEC4 = 821,
            LIST_ADD = 434,
            LIST_ITEM = 21,
            BOOL = 57,
            MATRIX4X4 = 560,
        }

        public enum OperationTableOpCode0xB
        {
            // Compare/operation types with A/B will have up to 2 stack values
            EqualsInt32,
            EqualsFloat32,
            EqualsStrings,
            EqualsInt2Pairs, // A == B && C == D
            NotEqualInt32,
            NotEqualFloat32,
            NotEqualStrings,
            NotEqualInt2Pairs, // A != B || C != D
            LessThanInt32, // A < B
            LessThanFloat32, // A < B
            LessThanString, // A < B (case-insensitive)
            LessThanEqualInt32, // A <= B
            LessThanEqualFloat32, // A <= B
            LessThanEqualStrings, // A <= B (case-insensitive)
            GreaterThanInt32, // A > B LAB_0077ca94
            GreaterThanFloat32, // A > B 
            GreaterThanStrings, // A > B (case-insensitive)
            GreaterThanEqualInt32, // A >= B
            GreaterThanEqualFloat32, // A >= B
            GreaterThanEqualStrings, // A >= B (case-insensitive)
            AddInt32s, // A + B 
            AddFloat32s, // A + B 
            SubInt32s, // A - B 
            SubFloat32s, // A - B 
            MulInt32s, // A * B 
            MulFloat32s, // A * B 
            DivideInt32s, // A / B 
            DivideFloat32s, // A / B 
            DivideUInt32s, // A / B 
            MaxInt32s, // max(A, B)
            MaxFloat32s, // max(A, B)
            MinInt32s, // min(A, B)
            MinFloat32s, // min(A, B)
            OrUInt32s, // A | B
            AndUInt32s, // A & B
            IsNotZero, // (A == 0) ? 1 : 0
            NegateInt32,  // A = -A
            NegateFloat32,  // A = -A
            DotProduct, // A * B + C * D
            LAB_0077c47c_AverageFloat, 
            CreateStringHash, // A = nlStringHash(A) //FUN_0077c708
            CreateLowerStringHash, // A = nlStringLowerHash(A) //FUN_0077cbac
            SetInt32,
            SignBit, // 0077cb5c
            SetZero, // A = 0 // LAB_0077ccb0


            LAB_0077c728, // stack[top] = globalArray16Bit[ stack[top] & 0xFFFF ];
            FUN_0077cb84, // Table lookup using top 2 stack values
        }
    }
}
