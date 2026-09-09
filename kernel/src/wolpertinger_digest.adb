with Interfaces;
with Ada.Streams;
with GNAT.SHA256;
with System.Storage_Elements;
with Wolpertinger_State_Encoding;

package body Wolpertinger_Digest is
   package SSE renames System.Storage_Elements;
   use type SSE.Storage_Offset;

   function State_Digest
     (State : Wolpertinger_State.Kernel_State)
      return Wolpertinger_Types.Byte_32
   is
      Encoded : constant SSE.Storage_Array :=
        Wolpertinger_State_Encoding.Encode (State);
      Input : Ada.Streams.Stream_Element_Array
        (1 .. Ada.Streams.Stream_Element_Offset (Encoded'Length));
      Result : Wolpertinger_Types.Byte_32 := [others => 0];
   begin
      for Offset in SSE.Storage_Offset range 0 .. Encoded'Length - 1 loop
         Input (Ada.Streams.Stream_Element_Offset (Offset + 1)) :=
           Ada.Streams.Stream_Element (Encoded (Encoded'First + Offset));
      end loop;
      declare
         Hash : constant GNAT.SHA256.Binary_Message_Digest := GNAT.SHA256.Digest (Input);
      begin
         for I in Result'Range loop
            Result (I) := Interfaces.Unsigned_8
              (Hash (Ada.Streams.Stream_Element_Offset (I)));
         end loop;
      end;
      return Result;
   end State_Digest;
end Wolpertinger_Digest;
