namespace NextLevelLibrary.LM2.Sections
{
    public class ScriptNew
    {
        public static readonly Dictionary<ScriptNew.OpCode, string> OpCodeDescriptions = new()
        {
            { ScriptNew.OpCode.LOAD_VAR, "Load global variable" },
            { ScriptNew.OpCode.LOAD_VAR_L, "Load global variable (long index)" },
            { ScriptNew.OpCode.LOAD_STR, "Load constant" },
            { ScriptNew.OpCode.LOAD_STR_L, "Load constant (long offset)" },
            { ScriptNew.OpCode.PUSH, "Push operand value onto stack" },
            { ScriptNew.OpCode.BRANCH_NZ, "Branch if non-zero and skip next instruction" },
            { ScriptNew.OpCode.JUMP, "Unconditional jump (changes instruction pointer)" },
            { ScriptNew.OpCode.CALL_NATIVE, "Call native function" },
            { ScriptNew.OpCode.CALL_NATIVE_L, "Call native function (long index)" },
            { ScriptNew.OpCode.CALL, "Call script function" },
            { ScriptNew.OpCode.CALLG, "Call global script function" },
            { ScriptNew.OpCode.CALLE, "Call extension" },
            { ScriptNew.OpCode.RETURN, "Return" },
            { ScriptNew.OpCode.RETS, "Return with stack adjust" },
            { ScriptNew.OpCode.LDL, "Load local variable" },
            { ScriptNew.OpCode.STL, "Store local variable" },
            { ScriptNew.OpCode.STPTR, "Write value to pointer (1/2/4/8 bytes)" },
            { ScriptNew.OpCode.LDPTR, "Read value from pointer (1/2/4/8 bytes)" },
            { ScriptNew.OpCode.PUSHS, "Push signed small immediate" },
            { ScriptNew.OpCode.LDO, "Load object or static field" },
            { ScriptNew.OpCode.LDF, "Load function pointer" },
            { ScriptNew.OpCode.ADDI, "Add immediate" },
            { ScriptNew.OpCode.ADDI_L, "Add immediate (long)" },
            { ScriptNew.OpCode.LDGA, "Load global address" },
            { ScriptNew.OpCode.LDGA_L, "Load global address (long)" },
            { ScriptNew.OpCode.LDLA, "Load local address" },
            { ScriptNew.OpCode.CALLM, "Call method" },
            { ScriptNew.OpCode.STG, "Store to global" },
            { ScriptNew.OpCode.LDGPtr, "Load from global" },
            { ScriptNew.OpCode.MEMZ, "Zero memory" },
            { ScriptNew.OpCode.SETL, "Set local (no pop)" },
            { ScriptNew.OpCode.MOVL, "Move local" },
            { ScriptNew.OpCode.MOVLP, "Move local and push" },
            { ScriptNew.OpCode.SETLC, "Set local constant" },
            { ScriptNew.OpCode.SETLI, "Set local immediate" },
            { ScriptNew.OpCode.PUSHR, "Push local range" },
            { ScriptNew.OpCode.PUSHT, "Push three locals" },
        };

        public enum OpCode
        {
            LOAD_VAR = 0x00, // Load variable by index
            LOAD_VAR_L = 0x01, // Load variable (long index)
            LOAD_STR = 0x02, // Load string
            LOAD_STR_L = 0x03, // Load string (long offset)

            PUSH = 0x04, // Push the operand value onto stack
            BRANCH_NZ = 0x05, // Branch if non-zero and skip next instruction
            JUMP = 0x06, // Unconditional jump (changes instruction pointer)
            CALL_NATIVE = 0x07, // Call native function
            CALL_NATIVE_L = 0x08, // Call native function (long index)
            CALL = 0x09, // Call script function
            CALLG = 0x0A, // Call global script function
            CALLE = 0x0B, // Call extension
            RETURN = 0x0C, // Return
            RETS = 0x0D, // Return with stack adjust
            LDL = 0x0E, // Load local
            STL = 0x0F, // Store local
            STPTR = 0x10, // Writes value to pointer (1/2/4/8 bytes)
            LDPTR = 0x11, // Reads value from pointer (1/2/4/8 bytes)
            PUSHS = 0x12, // Push signed small immediate
            LDO = 0x13, // Load object or static field
            LDF = 0x14, // Load function pointer
            ADDI = 0x15, // Add immediate
            ADDI_L = 0x16, // Add immediate (long)
            LDGA = 0x17, // Load global address
            LDGA_L = 0x18, // Load global address (long)
            LDLA = 0x19, // Load local address

            CALLM = 0x1C, // Call method 
            STG = 0x1D, // Store to global
            LDGPtr = 0x1E, // Load from global
            MEMZ = 0x1F, // Memory zero
            SETL = 0x20, // Set local (no pop)
            MOVL = 0x21, // Move local
            MOVLP = 0x22, // Move local and push
            SETLC = 0x23, // Set local constant
            SETLI = 0x24, // Set local immediate
            PUSHR = 0x25, // Push local range
            PUSHT = 0x26, // Push three locals
        }
    }
}
