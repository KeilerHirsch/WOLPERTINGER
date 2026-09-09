with Ada.Text_IO;
with AUnit.Assertions;
with Interfaces;
with System.Storage_Elements;
with Wolpertinger_Bounded_Text;
with Wolpertinger_Control;
with Wolpertinger_Protocol;
with Wolpertinger_State;
with Wolpertinger_Types;

procedure Test_Kernel_Messages is
   package Assert renames AUnit.Assertions;
   package Control renames Wolpertinger_Control;
   package Protocol renames Wolpertinger_Protocol;
   package State_Types renames Wolpertinger_State;
   package Text renames Wolpertinger_Bounded_Text;
   package Types renames Wolpertinger_Types;
   package SSE renames System.Storage_Elements;

   use type Interfaces.Integer_64;
   use type Interfaces.Unsigned_64;
   use type Control.Kernel_Role;
   use type Types.Observation_Kind;
   use type Protocol.Decode_Status;
   use type Protocol.Host_Message_Kind;
   use type SSE.Storage_Element;
   use type SSE.Storage_Offset;

   function Hex_Nibble (C : Character) return SSE.Storage_Element is
   begin
      case C is
         when '0' .. '9' => return SSE.Storage_Element (Character'Pos (C) - Character'Pos ('0'));
         when 'a' .. 'f' => return SSE.Storage_Element (10 + Character'Pos (C) - Character'Pos ('a'));
         when 'A' .. 'F' => return SSE.Storage_Element (10 + Character'Pos (C) - Character'Pos ('A'));
         when others => raise Constraint_Error with "invalid hex digit";
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

   procedure Assert_Bytes_Equal
     (Expected, Actual : SSE.Storage_Array; Message : String) is
   begin
      Assert.Assert (Expected'Length = Actual'Length, Message & " length");
      if Expected'Length = Actual'Length then
         for Offset in SSE.Storage_Offset range 0 .. Expected'Length - 1 loop
            Assert.Assert (Expected (Expected'First + Offset) = Actual (Actual'First + Offset),
                           Message & " byte" & SSE.Storage_Offset'Image (Offset));
         end loop;
      end if;
   end Assert_Bytes_Equal;

   function To_Bytes_32 (Data : SSE.Storage_Array) return Types.Byte_32 is
      Result : Types.Byte_32 := [others => 0];
   begin
      Assert.Assert (Data'Length = 32, "digest vector must be 32 bytes");
      for I in Result'Range loop
         Result (I) := Interfaces.Unsigned_8
           (Data (Data'First + SSE.Storage_Offset (I - 1)));
      end loop;
      return Result;
   end To_Bytes_32;

   procedure Decode_Set_Role is
      Data : constant SSE.Storage_Array := [16#A3#, 0, 1, 1, 1, 2, 1];
      Decoded : constant Protocol.Host_Decode_Result := Protocol.Decode_Host_Message (Data);
   begin
      Assert.Assert (Decoded.Status = Protocol.OK, "SetRole must decode");
      Assert.Assert (Decoded.Kind = Protocol.Set_Role_Message, "SetRole kind");
      Assert.Assert (Decoded.Epoch = 1, "SetRole epoch");
      Assert.Assert (Decoded.Role = Control.Active, "SetRole active role");
   end Decode_Set_Role;

   procedure Decode_Apply is
      Data : constant SSE.Storage_Array := Read_Hex ("../../fixtures/contracts/v1/fsdjump.hex");
      Decoded : constant Protocol.Host_Decode_Result := Protocol.Decode_Host_Message (Data);
   begin
      Assert.Assert (Decoded.Status = Protocol.OK, "ApplyObservation must decode");
      Assert.Assert (Decoded.Kind = Protocol.Apply_Observation_Message, "apply kind");
      Assert.Assert (Decoded.Observation.Kind = Types.FSD_Jump, "FSDJump observation kind");
   end Decode_Apply;
   procedure Encode_Role_Response is
      Expected : constant SSE.Storage_Array :=
        Read_Hex ("../../fixtures/contracts/v1/response-role-accepted.hex");
      Digest_Bytes : constant SSE.Storage_Array :=
        Read_Hex ("../../fixtures/contracts/v1/state-empty.sha256");
      Response : Protocol.Kernel_Response := (others => <>);
   begin
      Response.Kind := Protocol.Role_Response;
      Response.Status := Protocol.Response_OK;
      Response.Epoch := 1;
      Response.Role := Control.Active;
      Response.State_Digest := To_Bytes_32 (Digest_Bytes);
      Assert_Bytes_Equal (Expected, Protocol.Encode_Response (Response), "role response");
   end Encode_Role_Response;

   procedure Encode_FSDJump_Response is
      Expected : constant SSE.Storage_Array :=
        Read_Hex ("../../fixtures/contracts/v1/response-fsdjump-applied.hex");
      Digest_Bytes : constant SSE.Storage_Array :=
        Read_Hex ("../../fixtures/contracts/v1/state-populated.sha256");
      Response : Protocol.Kernel_Response := (others => <>);
   begin
      Response.Kind := Protocol.Apply_Response;
      Response.Status := Protocol.Response_OK;
      Response.Epoch := 1;
      Response.Role := Control.Active;
      Response.Has_Cursor := True;
      Response.Cursor := (Evidence_Sequence => 2, Message_Ordinal => 0);
      Response.State_Digest := To_Bytes_32 (Digest_Bytes);
      Response.Has_Jump_Fact := True;
      Response.Jump.Cursor := Response.Cursor;
      Response.Jump.System_Address := 1_234_567_890_123_456_789;
      Response.Jump.Star_System := Text.To_Text_128 ("W. Grantler NX-42");
      Response.Jump.Position :=
        (X => (Coefficient => 12_345, Exponent => -3),
         Y => (Coefficient => -6_789, Exponent => -2),
         Z => (Coefficient => 42, Exponent => 0));
      Response.Jump.Jump_Distance := (Coefficient => 55_359, Exponent => -3);
      Response.Jump.Fuel_Used := (Coefficient => 4_843_642, Exponent => -6);
      Response.Jump.Fuel_Level := (Coefficient => 27_123, Exponent => -3);
      Response.Jump.Location_Provenance := Types.Local_Journal;
      Response.Jump.Location_Freshness := State_Types.Current;
      Response.Jump.Fuel_Provenance := Types.Local_Journal;
      Response.Jump.Fuel_Freshness := State_Types.Current;
      Assert_Bytes_Equal (Expected, Protocol.Encode_Response (Response), "FSDJump response");
   end Encode_FSDJump_Response;

begin
   Decode_Set_Role;
   Decode_Apply;
   Encode_Role_Response;
   Encode_FSDJump_Response;
end Test_Kernel_Messages;
