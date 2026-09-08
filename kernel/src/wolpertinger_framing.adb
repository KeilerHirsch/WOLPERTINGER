with Interfaces;

package body Wolpertinger_Framing is
   use type Interfaces.Unsigned_32;
   use type SSE.Storage_Offset;

   function Parse_Frame
     (Data  : SSE.Storage_Array;
      First : SSE.Storage_Offset) return Parse_Result
   is
      Result : Parse_Result;
      Length : Interfaces.Unsigned_32;
   begin
      if First < Data'First or else First > Data'Last
        or else Data'Last - First + 1 < 4
      then
         return Result;
      end if;

      Length :=
        Interfaces.Unsigned_32 (Data (First)) * 16#1000000#
        + Interfaces.Unsigned_32 (Data (First + 1)) * 16#10000#
        + Interfaces.Unsigned_32 (Data (First + 2)) * 16#100#
        + Interfaces.Unsigned_32 (Data (First + 3));

      if Length = 0
        or else Length > Interfaces.Unsigned_32 (Maximum_Payload_Bytes)
      then
         Result.Status := Invalid_Length;
         return Result;
      end if;
      Result.Payload_First := First + 4;
      if Data'Last - Result.Payload_First + 1 < SSE.Storage_Offset (Length) then
         Result.Status := Truncated_Frame;
         return Result;
      end if;

      Result.Payload_Last := Result.Payload_First + SSE.Storage_Offset (Length) - 1;
      Result.Next_First := Result.Payload_Last + 1;
      Result.Status := OK;
      return Result;
   end Parse_Frame;

   function Encode_Frame
     (Payload : SSE.Storage_Array) return SSE.Storage_Array
   is
      Length : constant Interfaces.Unsigned_32 :=
        Interfaces.Unsigned_32 (Payload'Length);
      Result : SSE.Storage_Array (1 .. Payload'Length + 4);
   begin
      Result (1) := SSE.Storage_Element ((Length / 16#1000000#) mod 256);
      Result (2) := SSE.Storage_Element ((Length / 16#10000#) mod 256);
      Result (3) := SSE.Storage_Element ((Length / 16#100#) mod 256);
      Result (4) := SSE.Storage_Element (Length mod 256);
      for Offset in SSE.Storage_Offset range 0 .. Payload'Length - 1 loop
         Result (5 + Offset) := Payload (Payload'First + Offset);
      end loop;
      return Result;
   end Encode_Frame;
end Wolpertinger_Framing;
