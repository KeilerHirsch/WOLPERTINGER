with CBOR;
with CBOR.Decoding;
with CBOR.Encoding;
with Interfaces;
with Wolpertinger_Types;

package body Wolpertinger_State_Encoding is
   package Dec renames CBOR.Decoding;
   package Enc renames CBOR.Encoding;
   package State_Types renames Wolpertinger_State;
   package Types renames Wolpertinger_Types;

   use type Interfaces.Integer_64;
   use type System.Storage_Elements.Storage_Array;

   Zero_16 : constant Types.Byte_16 := [others => 0];
   Zero_32 : constant Types.Byte_32 := [others => 0];
   Zero_Decimal : constant Types.Decimal_64 :=
     (Coefficient => 0, Exponent => 0);

   function To_CBOR (Value : Types.Byte_16) return CBOR.Byte_Array is
      Result : CBOR.Byte_Array (1 .. 16);
   begin
      for I in Value'Range loop
         Result (CBOR.SE_Offset (I)) := CBOR.Byte (Value (I));
      end loop;
      return Result;
   end To_CBOR;
   function To_CBOR (Value : Types.Byte_32) return CBOR.Byte_Array is
      Result : CBOR.Byte_Array (1 .. 32);
   begin
      for I in Value'Range loop
         Result (CBOR.SE_Offset (I)) := CBOR.Byte (Value (I));
      end loop;
      return Result;
   end To_CBOR;

   function To_CBOR (Value : Types.Text_64) return CBOR.Byte_Array is
      Result : CBOR.Byte_Array (1 .. CBOR.SE_Offset (Value.Length));
   begin
      for I in 1 .. Value.Length loop
         Result (CBOR.SE_Offset (I)) := CBOR.Byte (Character'Pos (Value.Data (I)));
      end loop;
      return Result;
   end To_CBOR;

   function To_CBOR (Value : Types.Text_128) return CBOR.Byte_Array is
      Result : CBOR.Byte_Array (1 .. CBOR.SE_Offset (Value.Length));
   begin
      for I in 1 .. Value.Length loop
         Result (CBOR.SE_Offset (I)) := CBOR.Byte (Character'Pos (Value.Data (I)));
      end loop;
      return Result;
   end To_CBOR;

   function Encode_Decimal (Value : Types.Decimal_64) return CBOR.Byte_Array is
   begin
      if (Value.Coefficient = 0 and then Value.Exponent /= 0)
        or else (Value.Coefficient /= 0 and then Value.Coefficient mod 10 = 0)
      then
         raise Constraint_Error with "non-canonical Decimal64 in authoritative state";
      end if;
      return Enc.Encode_Array (2)
        & Enc.Encode_Integer (Value.Coefficient)
        & Enc.Encode_Integer (Interfaces.Integer_64 (Value.Exponent));
   end Encode_Decimal;
   function Encode_Text (Value : Types.Text_64) return CBOR.Byte_Array is
      Data : constant CBOR.Byte_Array := To_CBOR (Value);
   begin
      if not Dec.Is_Valid_UTF8 (Data) then
         raise Constraint_Error with "invalid UTF-8 in profile identity";
      end if;
      return Enc.Encode_Text_String_UTF8 (Data);
   end Encode_Text;

   function Encode_Text (Value : Types.Text_128) return CBOR.Byte_Array is
      Data : constant CBOR.Byte_Array := To_CBOR (Value);
   begin
      if not Dec.Is_Valid_UTF8 (Data) then
         raise Constraint_Error with "invalid UTF-8 in location state";
      end if;
      return Enc.Encode_Text_String_UTF8 (Data);
   end Encode_Text;

   function Encode
     (State : State_Types.Kernel_State)
      return System.Storage_Elements.Storage_Array
   is
      Session       : Types.Byte_16 := Zero_16;
      FID           : Types.Text_64 := (others => <>);
      Realm         : Types.Galaxy_Realm := Types.Unknown;
      Save_Epoch    : Interfaces.Unsigned_64 := 0;
      Last_Sequence      : Interfaces.Unsigned_64 := 0;
      Last_Ordinal       : Interfaces.Unsigned_32 := 0;
      Last_Message_Count : Interfaces.Unsigned_16 := 0;
      Last_Digest        : Types.Byte_32 := Zero_32;
      Address            : Interfaces.Unsigned_64 := 0;
      Star_System   : Types.Text_128 := (others => <>);
      Location_Prov : Types.Source_Provenance := Types.Unknown_Source;
      Location_Fresh : State_Types.Freshness_State := State_Types.Unknown;
      Fuel_Prov     : Types.Source_Provenance := Types.Unknown_Source;
      Fuel_Fresh    : State_Types.Freshness_State := State_Types.Unknown;
      Position      : Types.Galactic_Position :=
        (X => Zero_Decimal, Y => Zero_Decimal, Z => Zero_Decimal);
      Fuel_Level    : Types.Decimal_64 := Zero_Decimal;
      Fuel_Used     : Types.Decimal_64 := Zero_Decimal;
      Jump_Distance : Types.Decimal_64 := Zero_Decimal;
   begin
      if State.Bound then
         Session := State.Session_Id;
         FID := State.Profile.FID;
         Realm := State.Profile.Realm;
         Save_Epoch := State.Profile.Save_Epoch;
      end if;

      if State.Has_Last_Cursor then
         Last_Sequence := State.Last_Cursor.Evidence_Sequence;
         Last_Ordinal := State.Last_Cursor.Message_Ordinal;
         Last_Message_Count := State.Last_Message_Count;
         Last_Digest := State.Last_Digest;
      end if;

      if State.Location.Known then
         Address := State.Location.System_Address;
         Star_System := State.Location.Star_System;
         Location_Prov := State.Location.Provenance;
         Location_Fresh := State.Location.Freshness;
         Position := State.Location.Position;
         Jump_Distance := State.Last_Jump_Distance;
      end if;
      if State.Fuel.Known then
         Fuel_Prov := State.Fuel.Provenance;
         Fuel_Fresh := State.Fuel.Freshness;
         Fuel_Level := State.Fuel.Level;
         Fuel_Used := State.Fuel.Used;
      end if;

      return Enc.Encode_Array (23)
        & Enc.Encode_Unsigned (2)
        & Enc.Encode_Bool (State.Bound)
        & Enc.Encode_Byte_String (To_CBOR (Session))
        & Encode_Text (FID)
        & Enc.Encode_Unsigned (CBOR.UInt64 (Types.Galaxy_Realm'Pos (Realm)))
        & Enc.Encode_Unsigned (CBOR.UInt64 (Save_Epoch))
        & Enc.Encode_Bool (State.Has_Last_Cursor)
        & Enc.Encode_Unsigned (CBOR.UInt64 (Last_Sequence))
        & Enc.Encode_Unsigned (CBOR.UInt64 (Last_Ordinal))
        & Enc.Encode_Unsigned (CBOR.UInt64 (Last_Message_Count))
        & Enc.Encode_Byte_String (To_CBOR (Last_Digest))
        & Enc.Encode_Bool (State.Location.Known)
        & Enc.Encode_Unsigned (CBOR.UInt64 (Address))
        & Encode_Text (Star_System)
        & Enc.Encode_Unsigned (CBOR.UInt64 (Types.Source_Provenance'Pos (Location_Prov)))
        & Enc.Encode_Unsigned (CBOR.UInt64 (State_Types.Freshness_State'Pos (Location_Fresh)))
        & Enc.Encode_Bool (State.Fuel.Known)
        & Enc.Encode_Unsigned (CBOR.UInt64 (Types.Source_Provenance'Pos (Fuel_Prov)))
        & Enc.Encode_Unsigned (CBOR.UInt64 (State_Types.Freshness_State'Pos (Fuel_Fresh)))
        & Enc.Encode_Array (3)
        & Encode_Decimal (Position.X)
        & Encode_Decimal (Position.Y)
        & Encode_Decimal (Position.Z)
        & Encode_Decimal (Fuel_Level)
        & Encode_Decimal (Fuel_Used)
        & Encode_Decimal (Jump_Distance);
   end Encode;
end Wolpertinger_State_Encoding;
