with Ada.Text_IO;
with AUnit.Assertions;
with System.Storage_Elements;
with Wolpertinger_Framing;
with Wolpertinger_Protocol;

procedure Test_Framing is
   package Assert renames AUnit.Assertions;
   package Frame renames Wolpertinger_Framing;
   package Protocol renames Wolpertinger_Protocol;
   package SSE renames System.Storage_Elements;

   use type Frame.Frame_Status;
   use type Protocol.Decode_Status;
   use type SSE.Storage_Array;
   use type SSE.Storage_Element;
   use type SSE.Storage_Offset;

   function Header (Length : Natural) return SSE.Storage_Array is
      Result : SSE.Storage_Array (1 .. 4);
   begin
      Result (1) := SSE.Storage_Element ((Length / 16#1000000#) mod 256);
      Result (2) := SSE.Storage_Element ((Length / 16#10000#) mod 256);
      Result (3) := SSE.Storage_Element ((Length / 16#100#) mod 256);
      Result (4) := SSE.Storage_Element (Length mod 256);
      return Result;
   end Header;

   function Wire (Payload : SSE.Storage_Array) return SSE.Storage_Array is
     (Header (Natural (Payload'Length)) & Payload);
   function Hex_Nibble (C : Character) return SSE.Storage_Element is
   begin
      case C is
         when '0' .. '9' =>
            return SSE.Storage_Element (Character'Pos (C) - Character'Pos ('0'));
         when 'a' .. 'f' =>
            return SSE.Storage_Element (10 + Character'Pos (C) - Character'Pos ('a'));
         when 'A' .. 'F' =>
            return SSE.Storage_Element (10 + Character'Pos (C) - Character'Pos ('A'));
         when others =>
            raise Constraint_Error with "invalid hex digit";
      end case;
   end Hex_Nibble;

   function Read_Hex (Path : String) return SSE.Storage_Array is
      File : Ada.Text_IO.File_Type;
   begin
      Ada.Text_IO.Open (File, Ada.Text_IO.In_File, Path);
      declare
         Line : constant String := Ada.Text_IO.Get_Line (File);
         Data : SSE.Storage_Array (1 .. SSE.Storage_Offset (Line'Length / 2));
         P    : Positive := Line'First;
      begin
         Ada.Text_IO.Close (File);
         for I in Data'Range loop
            Data (I) := Hex_Nibble (Line (P)) * 16 + Hex_Nibble (Line (P + 1));
            P := P + 2;
         end loop;
         return Data;
      end;
   end Read_Hex;
   procedure Reject_Zero_Length is
      Data   : constant SSE.Storage_Array := Header (0);
      Parsed : constant Frame.Parse_Result := Frame.Parse_Frame (Data, Data'First);
   begin
      Assert.Assert (Parsed.Status = Frame.Invalid_Length, "zero-length frame must fail");
   end Reject_Zero_Length;

   procedure Reject_Oversize_Length is
      Data   : constant SSE.Storage_Array := Header (65_537);
      Parsed : constant Frame.Parse_Result := Frame.Parse_Frame (Data, Data'First);
   begin
      Assert.Assert (Parsed.Status = Frame.Invalid_Length, "oversize frame must fail");
   end Reject_Oversize_Length;

   procedure Reject_Truncated_Frame is
      Data   : constant SSE.Storage_Array := Header (2) & SSE.Storage_Array'(1 => 16#AA#);
      Parsed : constant Frame.Parse_Result := Frame.Parse_Frame (Data, Data'First);
   begin
      Assert.Assert (Parsed.Status = Frame.Truncated_Frame, "truncated payload must fail");
   end Reject_Truncated_Frame;

   procedure Reject_Trailing_CBOR is
      Golden  : constant SSE.Storage_Array := Read_Hex ("../../fixtures/contracts/v1/session-bound.hex");
      Payload : constant SSE.Storage_Array := Golden & SSE.Storage_Array'(1 => 0);
      Data    : constant SSE.Storage_Array := Wire (Payload);
      Parsed  : constant Frame.Parse_Result := Frame.Parse_Frame (Data, Data'First);
   begin
      Assert.Assert (Parsed.Status = Frame.OK, "framing should preserve declared payload");
      Assert.Assert
        (Protocol.Decode_Observation (Data (Parsed.Payload_First .. Parsed.Payload_Last)).Status /= Protocol.OK,
         "trailing bytes after complete CBOR must be rejected");
   end Reject_Trailing_CBOR;
   procedure Parse_Two_Consecutive_Frames is
      First_Payload  : constant SSE.Storage_Array := [1 => 16#11#];
      Second_Payload : constant SSE.Storage_Array := [1 => 16#22#, 2 => 16#33#];
      Data           : constant SSE.Storage_Array := Wire (First_Payload) & Wire (Second_Payload);
      First_Frame    : constant Frame.Parse_Result := Frame.Parse_Frame (Data, Data'First);
      Second_Frame   : constant Frame.Parse_Result := Frame.Parse_Frame (Data, First_Frame.Next_First);
   begin
      Assert.Assert (First_Frame.Status = Frame.OK, "first frame must parse");
      Assert.Assert (Second_Frame.Status = Frame.OK, "second frame must parse");
      Assert.Assert (Data (First_Frame.Payload_First) = 16#11#, "first payload must match");
      Assert.Assert (Second_Frame.Payload_Last - Second_Frame.Payload_First + 1 = 2,
                     "second payload length must match");
      Assert.Assert (Data (Second_Frame.Payload_First) = 16#22#, "second payload first byte");
      Assert.Assert (Data (Second_Frame.Payload_Last) = 16#33#, "second payload last byte");
      Assert.Assert (Second_Frame.Next_First = Data'Last + 1, "parser must end exactly after frame");
   end Parse_Two_Consecutive_Frames;

begin
   Reject_Zero_Length;
   Reject_Oversize_Length;
   Reject_Truncated_Frame;
   Reject_Trailing_CBOR;
   Parse_Two_Consecutive_Frames;
end Test_Framing;
