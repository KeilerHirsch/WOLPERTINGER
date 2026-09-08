with System.Storage_Elements;

package Wolpertinger_Framing is
   package SSE renames System.Storage_Elements;

   Maximum_Payload_Bytes : constant SSE.Storage_Offset := 65_536;

   type Frame_Status is (OK, Invalid_Length, Truncated_Frame);

   type Parse_Result is record
      Status        : Frame_Status := Truncated_Frame;
      Payload_First : SSE.Storage_Offset := 0;
      Payload_Last  : SSE.Storage_Offset := 0;
      Next_First    : SSE.Storage_Offset := 0;
   end record;

   function Parse_Frame
     (Data  : SSE.Storage_Array;
      First : SSE.Storage_Offset) return Parse_Result;

   function Encode_Frame
     (Payload : SSE.Storage_Array) return SSE.Storage_Array
     with Pre => Payload'Length in 1 .. Maximum_Payload_Bytes;
end Wolpertinger_Framing;
