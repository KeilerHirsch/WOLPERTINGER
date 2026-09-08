with Ada.Text_IO;
with AUnit.Assertions;
with Interfaces;
with System.Storage_Elements;
with Test_Engine;
with Test_Framing;
with Test_Control;
with Test_State_Digest;
with Test_Kernel_Messages;
with Wolpertinger_Protocol;
with Wolpertinger_Types;

procedure Wolpertinger_Kernel_Tests is
   package TIO renames Ada.Text_IO;
   package Assert renames AUnit.Assertions;
   package Protocol renames Wolpertinger_Protocol;
   package Types renames Wolpertinger_Types;
   package SSE renames System.Storage_Elements;

   use type Interfaces.Integer_64;
   use type Interfaces.Unsigned_64;
   use type SSE.Storage_Element;
   use type SSE.Storage_Offset;
   use type Protocol.Decode_Status;
   use type Types.Observation_Kind;

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
      File : TIO.File_Type;
   begin
      TIO.Open (File, TIO.In_File, Path);
      declare
         Line : constant String := TIO.Get_Line (File);
         Data : SSE.Storage_Array
           (1 .. SSE.Storage_Offset (Line'Length / 2));
         P : Positive := Line'First;
      begin
         TIO.Close (File);
         Assert.Assert (Line'Length mod 2 = 0, "hex vector must contain full bytes");
         for I in Data'Range loop
            Data (I) := Hex_Nibble (Line (P)) * 16 + Hex_Nibble (Line (P + 1));
            P := P + 2;
         end loop;
         return Data;
      end;
   end Read_Hex;

   procedure Assert_Bytes_Equal
     (Expected : SSE.Storage_Array;
      Actual   : SSE.Storage_Array;
      Message  : String)
   is
   begin
      Assert.Assert (Expected'Length = Actual'Length, Message & " length");
      if Expected'Length = Actual'Length then
         for Offset in SSE.Storage_Offset range 0 .. Expected'Length - 1 loop
            Assert.Assert
              (Expected (Expected'First + Offset) = Actual (Actual'First + Offset),
               Message & " byte" & SSE.Storage_Offset'Image (Offset));
         end loop;
      end if;
   end Assert_Bytes_Equal;

   procedure Test_Session_Bound is
      Data : constant SSE.Storage_Array :=
        Read_Hex ("../../fixtures/contracts/v1/session-bound.hex");
      Decoded : constant Protocol.Decode_Result := Protocol.Decode_Observation (Data);
   begin
      TIO.Put_Line ("SessionBound decode=" & Protocol.Decode_Status'Image (Decoded.Status));
      Assert.Assert (Decoded.Status = Protocol.OK, "SessionBound must decode");
      Assert.Assert
        (Decoded.Observation.Kind = Types.Session_Bound,
         "SessionBound kind must match");
      Assert.Assert
        (Decoded.Observation.Cursor.Evidence_Sequence = 1,
         "SessionBound sequence must be 1");
      Assert.Assert
        (Decoded.Observation.Profile.FID.Length = 9,
         "FID length must match");
      Assert.Assert
        (Decoded.Observation.Profile.FID.Data (1 .. 9) = "FTEST0001",
         "FID must match");
      declare
         Encoded : constant SSE.Storage_Array :=
           Protocol.Encode_Observation (Decoded.Observation);
      begin
         Assert_Bytes_Equal (Data, Encoded, "SessionBound golden vector");
      end;
   end Test_Session_Bound;

   procedure Test_FSD_Jump is
      Data : constant SSE.Storage_Array :=
        Read_Hex ("../../fixtures/contracts/v1/fsdjump.hex");
      Decoded : constant Protocol.Decode_Result := Protocol.Decode_Observation (Data);
   begin
      TIO.Put_Line ("FSDJump decode=" & Protocol.Decode_Status'Image (Decoded.Status));
      Assert.Assert (Decoded.Status = Protocol.OK, "FSDJump must decode");
      Assert.Assert
        (Decoded.Observation.Kind = Types.FSD_Jump,
         "FSDJump kind must match");
      Assert.Assert
        (Decoded.Observation.Cursor.Evidence_Sequence = 2,
         "FSDJump sequence must be 2");
      Assert.Assert
        (Decoded.Observation.Jump.System_Address = 1_234_567_890_123_456_789,
         "SystemAddress must match");
      Assert.Assert
        (Decoded.Observation.Jump.Star_System.Data (1 .. 17) = "W. Grantler NX-42",
         "StarSystem must match");
      Assert.Assert
        (Decoded.Observation.Jump.Jump_Distance.Coefficient = 55_359,
         "JumpDistance coefficient must match");
      Assert.Assert
        (Decoded.Observation.Jump.Jump_Distance.Exponent = -3,
         "JumpDistance exponent must match");
      declare
         Encoded : constant SSE.Storage_Array :=
           Protocol.Encode_Observation (Decoded.Observation);
      begin
         Assert_Bytes_Equal (Data, Encoded, "FSDJump golden vector");
      end;
   end Test_FSD_Jump;

   procedure Test_Oversize_Rejected_Before_Parse is
      Data : constant SSE.Storage_Array
        (1 .. SSE.Storage_Offset (65_537)) := [others => 0];
      Decoded : constant Protocol.Decode_Result := Protocol.Decode_Observation (Data);
   begin
      TIO.Put_Line ("Oversize decode=" & Protocol.Decode_Status'Image (Decoded.Status));
      Assert.Assert
        (Decoded.Status = Protocol.Resource_Limit,
         "oversize frame must be rejected at resource boundary");
   end Test_Oversize_Rejected_Before_Parse;
begin
   TIO.Put_Line ("WOLPERTINGER kernel protocol v1 tests");
   Test_Session_Bound;
   Test_FSD_Jump;
   Test_Oversize_Rejected_Before_Parse;
   Test_Engine;
   Test_Framing;
   Test_Control;
   Test_State_Digest;
   Test_Kernel_Messages;
   TIO.Put_Line ("PASS: protocol + engine + framing + digest + control tests");
end Wolpertinger_Kernel_Tests;
