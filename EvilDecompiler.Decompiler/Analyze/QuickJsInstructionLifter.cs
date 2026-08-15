using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.Decompiler.Instruction;
using EvilDecompiler.JsObject.Types.Objects;
using EvilDecompiler.JsObject.Types;
using static EvilDecompiler.ByteCode.Type.QuickJsOPCode;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.ByteCode.Operand;

namespace EvilDecompiler.Decompiler.Analyze
{
    public class QuickJsInstructionLifter
    {

        private QuickJsInstruction[] quickJsInstructions;

        public QuickJsInstructionLifter(QuickJsInstruction[] instructions)
        {
            quickJsInstructions = instructions;
        }

        public QuickJsInstruction[] LiftToIR()
        {

            List<QuickJsInstruction> list = new List<QuickJsInstruction>();

            for (int i = 0; i < quickJsInstructions.Length; i++)
            {

                QuickJsInstruction result;
                QuickJsInstruction ins = quickJsInstructions[i];

                QuickJsOPCode opcode = ins.getOpCode();
                long pc = ins.getPC();
                QuickJsOperand operand = ins.getOperand();
                JsFunctionBytecode jsFunctionBytecode = ins.FunctionBytecode;
                AtomSet atoms = ins.AtomSet;

                switch (opcode.OPCode)
                {

                    case OPCodeValue.OP_push_minus1:
                    case OPCodeValue.OP_push_0:
                    case OPCodeValue.OP_push_1:
                    case OPCodeValue.OP_push_2:
                    case OPCodeValue.OP_push_3:
                    case OPCodeValue.OP_push_4:
                    case OPCodeValue.OP_push_5:
                    case OPCodeValue.OP_push_6:
                    case OPCodeValue.OP_push_7:
                    case OPCodeValue.OP_push_i8:
                    case OPCodeValue.OP_push_i16:
                    case OPCodeValue.OP_push_i32:
                    case OPCodeValue.OP_push_this:
                    case OPCodeValue.OP_push_true:
                    case OPCodeValue.OP_push_false:
                    case OPCodeValue.OP_push_const:
                    case OPCodeValue.OP_push_const8:
                    case OPCodeValue.OP_push_atom_value:
                    case OPCodeValue.OP_push_empty_string:
                        result = new QuickJsInstructionPushValue(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;

                    case OPCodeValue.OP_get_arg:
                    case OPCodeValue.OP_get_arg0:
                    case OPCodeValue.OP_get_arg1:
                    case OPCodeValue.OP_get_arg2:
                    case OPCodeValue.OP_get_arg3:
                    case OPCodeValue.OP_get_loc:
                    case OPCodeValue.OP_get_loc0:
                    case OPCodeValue.OP_get_loc1:
                    case OPCodeValue.OP_get_loc2:
                    case OPCodeValue.OP_get_loc3:
                    case OPCodeValue.OP_get_loc8:
                    case OPCodeValue.OP_get_var:
                    case OPCodeValue.OP_get_var_undef:
                    case OPCodeValue.OP_get_var_ref: // 暂时不知道var ref是咋和外面的变量对应的先写这里
                    case OPCodeValue.OP_get_var_ref0:
                    case OPCodeValue.OP_get_var_ref1:
                    case OPCodeValue.OP_get_var_ref2:
                    case OPCodeValue.OP_get_var_ref3:
                    case OPCodeValue.OP_get_var_ref_check:
                    case OPCodeValue.OP_get_loc_check:
                        result = new QuickJsInstructionGetVar(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;

                    case OPCodeValue.OP_get_array_el:
                    case OPCodeValue.OP_get_array_el2:
                    case OPCodeValue.OP_get_length:
                    case OPCodeValue.OP_get_field:
                    case OPCodeValue.OP_get_field2:
                        result = new QuickJsInstructionGetProperty(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;

                    case OPCodeValue.OP_put_arg:
                    case OPCodeValue.OP_put_arg0:
                    case OPCodeValue.OP_put_arg1:
                    case OPCodeValue.OP_put_arg2:
                    case OPCodeValue.OP_put_arg3:
                    case OPCodeValue.OP_put_loc:
                    case OPCodeValue.OP_put_loc0:
                    case OPCodeValue.OP_put_loc1:
                    case OPCodeValue.OP_put_loc2:
                    case OPCodeValue.OP_put_loc3:
                    case OPCodeValue.OP_put_loc8:
                    case OPCodeValue.OP_put_var:
                    case OPCodeValue.OP_put_var_init:
                    case OPCodeValue.OP_put_var_ref:
                    case OPCodeValue.OP_put_var_ref0:
                    case OPCodeValue.OP_put_var_ref1:
                    case OPCodeValue.OP_put_var_ref2:
                    case OPCodeValue.OP_put_var_ref3:
                    case OPCodeValue.OP_put_loc_check:
                    case OPCodeValue.OP_put_loc_check_init:

                    case OPCodeValue.OP_set_arg:
                    case OPCodeValue.OP_set_arg0:
                    case OPCodeValue.OP_set_arg1:
                    case OPCodeValue.OP_set_arg2:
                    case OPCodeValue.OP_set_arg3:
                    case OPCodeValue.OP_set_loc:
                    case OPCodeValue.OP_set_loc0:
                    case OPCodeValue.OP_set_loc1:
                    case OPCodeValue.OP_set_loc2:
                    case OPCodeValue.OP_set_loc3:
                    case OPCodeValue.OP_set_loc8:
                    case OPCodeValue.OP_set_var_ref:
                    case OPCodeValue.OP_set_var_ref0:
                    case OPCodeValue.OP_set_var_ref1:
                    case OPCodeValue.OP_set_var_ref2:
                    case OPCodeValue.OP_set_var_ref3:
                    case OPCodeValue.OP_set_name:
                    case OPCodeValue.OP_set_loc_uninitialized:
                    case OPCodeValue.OP_define_field:
                        result = new QuickJsInstructionSetVar(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;

                    case OPCodeValue.OP_dup:
                    case OPCodeValue.OP_dup1:
                    case OPCodeValue.OP_dup2:
                    case OPCodeValue.OP_dup3:
                        result = new QuickJsInstructionDup(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;

                    case OPCodeValue.OP_drop:
                        result = new QuickJsInstructionPop(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;

                    case OPCodeValue.OP_fclosure:
                    case OPCodeValue.OP_fclosure8:
                        result = new QuickJsInstructionClosure(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;

                    case OPCodeValue.OP_typeof:
                    case OPCodeValue.OP_typeof_is_function:
                    case OPCodeValue.OP_typeof_is_undefined:
                        result = new QuickJsInstructionTypeOf(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;

                    case OPCodeValue.OP_lt:
                    case OPCodeValue.OP_lte:
                    case OPCodeValue.OP_gt:
                    case OPCodeValue.OP_gte:
                    case OPCodeValue.OP_eq:
                    case OPCodeValue.OP_neq:
                    case OPCodeValue.OP_strict_neq:
                    case OPCodeValue.OP_strict_eq:
                        result = new QuickJsInstructionCompare(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;

                    case OPCodeValue.OP_return:
                    case OPCodeValue.OP_return_async:
                    case OPCodeValue.OP_return_undef:
                        result = new QuickJsInstructionReturn(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;


                    case OPCodeValue.OP_mul:
                    case OPCodeValue.OP_div:
                    case OPCodeValue.OP_mod:
                    case OPCodeValue.OP_add:
                    case OPCodeValue.OP_sub:
                    case OPCodeValue.OP_pow:
                    case OPCodeValue.OP_shl:
                    case OPCodeValue.OP_sar:
                    case OPCodeValue.OP_shr:
                    case OPCodeValue.OP_xor:
                    case OPCodeValue.OP_and:
                    case OPCodeValue.OP_or:
                        result = new QuickJsInstructionMath(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;

                    case OPCodeValue.OP_call_constructor:
                    case OPCodeValue.OP_call_method:
                    case OPCodeValue.OP_tail_call_method:
                        result = new QuickJsInstructionCall(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;


                    case OPCodeValue.OP_object:
                    case OPCodeValue.OP_special_object:
                    case OPCodeValue.OP_array_from:
                        result = new QuickJsInstructionObject(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;

                    case OPCodeValue.OP_throw:
                    case OPCodeValue.OP_throw_error:
                        result = new QuickJsInstructionThrow(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;

                    default:
                        result = new QuickJsInstruction(pc, opcode, operand, jsFunctionBytecode, atoms);
                        break;
                }

                list.Add(result);
            }

            return list.ToArray();
        }

    }
}
