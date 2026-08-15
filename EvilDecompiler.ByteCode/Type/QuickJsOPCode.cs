namespace EvilDecompiler.ByteCode.Type
{

    public class QuickJsOPCode
    {

        public enum OPCodeValue
        {
            OP_invalid,
            OP_push_i32,
            OP_push_const,
            OP_fclosure,
            OP_push_atom_value,
            OP_private_symbol,
            OP_undefined,
            OP_null,
            OP_push_this,
            OP_push_false,
            OP_push_true,
            OP_object,
            OP_special_object,
            OP_rest,
            OP_drop,
            OP_nip,
            OP_nip1,
            OP_dup,
            OP_dup1,
            OP_dup2,
            OP_dup3,
            OP_insert2,
            OP_insert3,
            OP_insert4,
            OP_perm3,
            OP_perm4,
            OP_perm5,
            OP_swap,
            OP_swap2,
            OP_rot3l,
            OP_rot3r,
            OP_rot4l,
            OP_rot5l,
            OP_call_constructor,
            OP_call,
            OP_tail_call,
            OP_call_method,
            OP_tail_call_method,
            OP_array_from,
            OP_apply,
            OP_return,
            OP_return_undef,
            OP_check_ctor_return,
            OP_check_ctor,
            OP_check_brand,
            OP_add_brand,
            OP_return_async,
            OP_throw,
            OP_throw_error,
            OP_eval,
            OP_apply_eval,
            OP_regexp,
            OP_get_super,
            OP_import,
            OP_check_var,
            OP_get_var_undef,
            OP_get_var,
            OP_put_var,
            OP_put_var_init,
            OP_put_var_strict,
            OP_get_ref_value,
            OP_put_ref_value,
            OP_define_var,
            OP_check_define_var,
            OP_define_func,
            OP_get_field,
            OP_get_field2,
            OP_put_field,
            OP_get_private_field,
            OP_put_private_field,
            OP_define_private_field,
            OP_get_array_el,
            OP_get_array_el2,
            OP_put_array_el,
            OP_get_super_value,
            OP_put_super_value,
            OP_define_field,
            OP_set_name,
            OP_set_name_computed,
            OP_set_proto,
            OP_set_home_object,
            OP_define_array_el,
            OP_append,
            OP_copy_data_properties,
            OP_define_method,
            OP_define_method_computed,
            OP_define_class,
            OP_define_class_computed,
            OP_get_loc,
            OP_put_loc,
            OP_set_loc,
            OP_get_arg,
            OP_put_arg,
            OP_set_arg,
            OP_get_var_ref,
            OP_put_var_ref,
            OP_set_var_ref,
            OP_set_loc_uninitialized,
            OP_get_loc_check,
            OP_put_loc_check,
            OP_put_loc_check_init,
            OP_get_var_ref_check,
            OP_put_var_ref_check,
            OP_put_var_ref_check_init,
            OP_close_loc,
            OP_if_false,
            OP_if_true,
            OP_goto,
            OP_catch,
            OP_gosub,
            OP_ret,
            OP_to_object,
            OP_to_propkey,
            OP_to_propkey2,
            OP_with_get_var,
            OP_with_put_var,
            OP_with_delete_var,
            OP_with_make_ref,
            OP_with_get_ref,
            OP_with_get_ref_undef,
            OP_make_loc_ref,
            OP_make_arg_ref,
            OP_make_var_ref_ref,
            OP_make_var_ref,
            OP_for_in_start,
            OP_for_of_start,
            OP_for_await_of_start,
            OP_for_in_next,
            OP_for_of_next,
            OP_iterator_check_object,
            OP_iterator_get_value_done,
            OP_iterator_close,
            OP_iterator_close_return,
            OP_iterator_next,
            OP_iterator_call,
            OP_initial_yield,
            OP_yield,
            OP_yield_star,
            OP_async_yield_star,
            OP_await,
            OP_neg,
            OP_plus,
            OP_dec,
            OP_inc,
            OP_post_dec,
            OP_post_inc,
            OP_dec_loc,
            OP_inc_loc,
            OP_add_loc,
            OP_not,
            OP_lnot,
            OP_typeof,
            OP_delete,
            OP_delete_var,
            OP_mul,
            OP_div,
            OP_mod,
            OP_add,
            OP_sub,
            OP_pow,
            OP_shl,
            OP_sar,
            OP_shr,
            OP_lt,
            OP_lte,
            OP_gt,
            OP_gte,
            OP_instanceof,
            OP_in,
            OP_eq,
            OP_neq,
            OP_strict_eq,
            OP_strict_neq,
            OP_and,
            OP_xor,
            OP_or,
            OP_is_undefined_or_null,
            OP_nop,
            OP_push_minus1,
            OP_push_0,
            OP_push_1,
            OP_push_2,
            OP_push_3,
            OP_push_4,
            OP_push_5,
            OP_push_6,
            OP_push_7,
            OP_push_i8,
            OP_push_i16,
            OP_push_const8,
            OP_fclosure8,
            OP_push_empty_string,
            OP_get_loc8,
            OP_put_loc8,
            OP_set_loc8,
            OP_get_loc0,
            OP_get_loc1,
            OP_get_loc2,
            OP_get_loc3,
            OP_put_loc0,
            OP_put_loc1,
            OP_put_loc2,
            OP_put_loc3,
            OP_set_loc0,
            OP_set_loc1,
            OP_set_loc2,
            OP_set_loc3,
            OP_get_arg0,
            OP_get_arg1,
            OP_get_arg2,
            OP_get_arg3,
            OP_put_arg0,
            OP_put_arg1,
            OP_put_arg2,
            OP_put_arg3,
            OP_set_arg0,
            OP_set_arg1,
            OP_set_arg2,
            OP_set_arg3,
            OP_get_var_ref0,
            OP_get_var_ref1,
            OP_get_var_ref2,
            OP_get_var_ref3,
            OP_put_var_ref0,
            OP_put_var_ref1,
            OP_put_var_ref2,
            OP_put_var_ref3,
            OP_set_var_ref0,
            OP_set_var_ref1,
            OP_set_var_ref2,
            OP_set_var_ref3,
            OP_get_length,
            OP_if_false8,
            OP_if_true8,
            OP_goto8,
            OP_goto16,
            OP_call0,
            OP_call1,
            OP_call2,
            OP_call3,
            OP_is_undefined,
            OP_is_null,
            OP_typeof_is_undefined,
            OP_typeof_is_function
        }

        public OPCodeValue OPCode = OPCodeValue.OP_invalid;

        public string Name
        {
            get
            {
                return QuickJsOPCodeInfo.Info[(int)OPCode].Name;
            }
        }

        public int Size
        {
            get
            {
                return QuickJsOPCodeInfo.Info[(int)OPCode].Size;
            }
        }

        public int PopCount
        {
            get
            {
                return QuickJsOPCodeInfo.Info[(int)OPCode].PopCount;
            }
        }

        public int PushCount
        {
            get
            {
                return QuickJsOPCodeInfo.Info[(int)OPCode].PushCount;
            }
        }

        public QuickJsOPCodeFormat Format
        {
            get
            {
                return QuickJsOPCodeInfo.Info[(int)OPCode].Format;
            }
        }

        public QuickJsOPCode(int opCode)
        {
            OPCode = (OPCodeValue) opCode;

            if (OPCodeValue.OP_invalid > OPCode || OPCode > OPCodeValue.OP_typeof_is_function)
                OPCode = OPCodeValue.OP_invalid;
        }

    }

}
