with Ada.Text_IO;
with AUnit.Assertions;
with Interfaces;
with System.Storage_Elements;
with Wolpertinger_Bounded_Text;
with Wolpertinger_Digest;
with Wolpertinger_State;
with Wolpertinger_State_Encoding;
with Wolpertinger_Types;

procedure Test_State_Digest is
   package Assert renames AUnit.Assertions;
   package Digest renames Wolpertinger_Digest;
   package State_Encoding renames Wolpertinger_State_Encoding;
   package State_Types renames Wolpertinger_State;
   package Text renames Wolpertinger_Bounded_Text;
   package Types renames Wolpertinger_Types;
   package SSE renames System.Storage_Elements;

   use type Interfaces.Integer_64;
   use type Types.Byte_32;
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
            Assert.Assert
              (Expected (Expected'First + Offset) = Actual (Actual'First + Offset),
               Message & " byte" & SSE.Storage_Offset'Image (Offset));
         end loop;
      end if;
   end Assert_Bytes_Equal;
   function Populated_State return State_Types.Kernel_State is
      State : State_Types.Kernel_State := (others => <>);
   begin
      State.Bound := True;
      for I in State.Session_Id'Range loop
         State.Session_Id (I) := Interfaces.Unsigned_8 (16#9F# + I);
      end loop;
      State.Profile.FID := Text.To_Text_64 ("FTEST0001");
      State.Profile.Realm := Types.Live;
      State.Profile.Save_Epoch := 0;
      State.Has_Last_Cursor := True;
      State.Last_Cursor := (Evidence_Sequence => 2, Message_Ordinal => 0);
      State.Location.Known := True;
      State.Location.System_Address := 1_234_567_890_123_456_789;
      State.Location.Star_System := Text.To_Text_128 ("W. Grantler NX-42");
      State.Location.Position :=
        (X => (Coefficient => 12_345, Exponent => -3),
         Y => (Coefficient => -6_789, Exponent => -2),
         Z => (Coefficient => 42, Exponent => 0));
      State.Location.Provenance := Types.Local_Journal;
      State.Location.Freshness := State_Types.Current;
      State.Fuel.Known := True;
      State.Fuel.Level := (Coefficient => 27_123, Exponent => -3);
      State.Fuel.Used := (Coefficient => 4_843_642, Exponent => -6);
      State.Fuel.Provenance := Types.Local_Journal;
      State.Fuel.Freshness := State_Types.Current;
      State.Last_Jump.Jump_Distance := (Coefficient => 55_359, Exponent => -3);
      return State;
   end Populated_State;
   function Session_State return State_Types.Kernel_State is
      State : State_Types.Kernel_State := (others => <>);
   begin
      State.Bound := True;
      for I in State.Session_Id'Range loop
         State.Session_Id (I) := Interfaces.Unsigned_8 (16#9F# + I);
      end loop;
      State.Profile.FID := Text.To_Text_64 ("FTEST0001");
      State.Profile.Realm := Types.Live;
      State.Profile.Save_Epoch := 0;
      State.Has_Last_Cursor := True;
      State.Last_Cursor := (Evidence_Sequence => 1, Message_Ordinal => 0);
      return State;
   end Session_State;
   procedure Empty_State_Uses_Neutral_Positions is
      State    : constant State_Types.Kernel_State := (others => <>);
      Expected : constant SSE.Storage_Array :=
        Read_Hex ("../../fixtures/contracts/v1/state-empty.hex");
   begin
      Assert_Bytes_Equal (Expected, State_Encoding.Encode (State), "empty state canonical bytes");
   end Empty_State_Uses_Neutral_Positions;

   procedure Populated_State_Matches_Golden_Bytes_And_Digest is
      State       : constant State_Types.Kernel_State := Populated_State;
      Expected    : constant SSE.Storage_Array :=
        Read_Hex ("../../fixtures/contracts/v1/state-populated.hex");
      Expected_Hash : constant SSE.Storage_Array :=
        Read_Hex ("../../fixtures/contracts/v1/state-populated.sha256");
      Actual_Hash : constant Types.Byte_32 := Digest.State_Digest (State);
   begin
      Assert_Bytes_Equal (Expected, State_Encoding.Encode (State), "populated state canonical bytes");
      for I in Actual_Hash'Range loop
         Assert.Assert
           (SSE.Storage_Element (Actual_Hash (I)) =
              Expected_Hash (Expected_Hash'First + SSE.Storage_Offset (I - 1)),
            "state digest byte" & Integer'Image (I));
      end loop;
   end Populated_State_Matches_Golden_Bytes_And_Digest;

   procedure Fuel_Used_Is_Part_Of_State_Identity is
      Left  : State_Types.Kernel_State := Populated_State;
      Right : State_Types.Kernel_State := Populated_State;
   begin
      Left.Fuel.Used := (Coefficient => 4_843_642, Exponent => -6);
      Right.Fuel.Used := (Coefficient => 4_843_643, Exponent => -6);
      Assert.Assert
        (Digest.State_Digest (Left) /= Digest.State_Digest (Right),
         "Fuel.Used must affect authoritative state digest");
   end Fuel_Used_Is_Part_Of_State_Identity;

   procedure Session_State_Matches_Golden_Bytes_And_Digest is
      State         : constant State_Types.Kernel_State := Session_State;
      Expected      : constant SSE.Storage_Array :=
        Read_Hex ("../../fixtures/contracts/v1/state-session-bound.hex");
      Expected_Hash : constant SSE.Storage_Array :=
        Read_Hex ("../../fixtures/contracts/v1/state-session-bound.sha256");
      Actual_Hash   : constant Types.Byte_32 := Digest.State_Digest (State);
   begin
      Assert_Bytes_Equal (Expected, State_Encoding.Encode (State), "session state canonical bytes");
      for I in Actual_Hash'Range loop
         Assert.Assert
           (SSE.Storage_Element (Actual_Hash (I)) =
              Expected_Hash (Expected_Hash'First + SSE.Storage_Offset (I - 1)),
            "session digest byte" & Integer'Image (I));
      end loop;
   end Session_State_Matches_Golden_Bytes_And_Digest;
begin
   Empty_State_Uses_Neutral_Positions;
   Session_State_Matches_Golden_Bytes_And_Digest;
   Populated_State_Matches_Golden_Bytes_And_Digest;
   Fuel_Used_Is_Part_Of_State_Identity;
end Test_State_Digest;
