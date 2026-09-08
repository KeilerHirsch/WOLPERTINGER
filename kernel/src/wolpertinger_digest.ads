with Wolpertinger_State;
with Wolpertinger_Types;

package Wolpertinger_Digest is
   function State_Digest
     (State : Wolpertinger_State.Kernel_State)
      return Wolpertinger_Types.Byte_32;
end Wolpertinger_Digest;
