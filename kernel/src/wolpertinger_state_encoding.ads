with System.Storage_Elements;
with Wolpertinger_State;

package Wolpertinger_State_Encoding is
   function Encode
     (State : Wolpertinger_State.Kernel_State)
      return System.Storage_Elements.Storage_Array;
end Wolpertinger_State_Encoding;
