with CBOR.Decoding;
with CBOR.Encoding;
with System.Storage_Elements;

package body Wolpertinger_Protocol is

   package Dec renames CBOR.Decoding;
   package Enc renames CBOR.Encoding;
   package Types renames Wolpertinger_Types;
   package SSE renames System.Storage_Elements;

   use type CBOR.Decode_Status;
   use type CBOR.Item_Count;
   use type CBOR.Major_Type;
   use type CBOR.SE_Offset;
   use type CBOR.UInt64;
   use type SSE.Storage_Array;
   use type Interfaces.Integer_64;
   use type Interfaces.Unsigned_8;
   use type Interfaces.Unsigned_16;

   Schema_Error : exception;

   function Is_Resource_Error
     (Status : CBOR.Decode_Status) return Boolean is
     (Status = CBOR.Err_Depth_Exceeded
      or else Status = CBOR.Err_Too_Many_Items
      or else Status = CBOR.Err_String_Too_Long
      or else Status = CBOR.Err_Resource_Limit);

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
         Result (CBOR.SE_Offset (I)) :=
           CBOR.Byte (Character'Pos (Value.Data (I)));
      end loop;
      return Result;
   end To_CBOR;

   function To_CBOR (Value : Types.Text_128) return CBOR.Byte_Array is
      Result : CBOR.Byte_Array (1 .. CBOR.SE_Offset (Value.Length));
   begin
      for I in 1 .. Value.Length loop
         Result (CBOR.SE_Offset (I)) :=
           CBOR.Byte (Character'Pos (Value.Data (I)));
      end loop;
      return Result;
   end To_CBOR;

   function Encode_Signed
     (Value : Interfaces.Integer_64) return CBOR.Byte_Array is
     (Enc.Encode_Integer (Value));

   function Encode_Decimal
     (Value : Types.Decimal_64) return CBOR.Byte_Array is
   begin
      if (Value.Coefficient = 0 and then Value.Exponent /= 0)
        or else (Value.Coefficient /= 0
                 and then Value.Coefficient mod 10 = 0)
      then
         raise Constraint_Error with "non-canonical Decimal64";
      end if;

      return Enc.Encode_Array (2)
        & Encode_Signed (Value.Coefficient)
        & Encode_Signed (Interfaces.Integer_64 (Value.Exponent));
   end Encode_Decimal;

   function Encode_FSD_Jump
     (Value : Types.FSD_Jump_Data) return CBOR.Byte_Array is
      Star : constant CBOR.Byte_Array := To_CBOR (Value.Star_System);
   begin
      if Value.Star_System.Length not in 1 .. 128
        or else not Dec.Is_Valid_UTF8 (Star)
      then
         raise Constraint_Error with "invalid StarSystem";
      end if;

      return Enc.Encode_Map (6)
        & Enc.Encode_Unsigned (0)
        & Enc.Encode_Text_String_UTF8 (Star)
        & Enc.Encode_Unsigned (1)
        & Enc.Encode_Unsigned (CBOR.UInt64 (Value.System_Address))
        & Enc.Encode_Unsigned (2)
        & Enc.Encode_Array (3)
        & Encode_Decimal (Value.Position.X)
        & Encode_Decimal (Value.Position.Y)
        & Encode_Decimal (Value.Position.Z)
        & Enc.Encode_Unsigned (3)
        & Encode_Decimal (Value.Jump_Distance)
        & Enc.Encode_Unsigned (4)
        & Encode_Decimal (Value.Fuel_Used)
        & Enc.Encode_Unsigned (5)
        & Encode_Decimal (Value.Fuel_Level);
   end Encode_FSD_Jump;

   function Encode_Source_Time
     (Value : Types.Observation) return CBOR.Byte_Array is
   begin
      if Value.Source_Time_Present then
         return Encode_Signed (Value.Source_Time_Unix_Ms);
      else
         return Enc.Encode_Null;
      end if;
   end Encode_Source_Time;

   function Encode_Payload
     (Value : Types.Observation) return CBOR.Byte_Array is
   begin
      case Value.Kind is
         when Types.Session_Bound =>
            return Enc.Encode_Null;
         when Types.FSD_Jump =>
            return Encode_FSD_Jump (Value.Jump);
      end case;
   end Encode_Payload;

   function Encode_Observation
     (Value : Types.Observation) return CBOR.Byte_Array is
      FID : constant CBOR.Byte_Array := To_CBOR (Value.Profile.FID);
   begin
      if Value.Profile.FID.Length not in 1 .. 64
        or else not Dec.Is_Valid_UTF8 (FID)
        or else Value.Message_Count = 0
      then
         raise Constraint_Error with "invalid observation bounds";
      end if;
      return Enc.Encode_Map (13)
        & Enc.Encode_Unsigned (0) & Enc.Encode_Unsigned (2)
        & Enc.Encode_Unsigned (1) & Enc.Encode_Unsigned (1)
        & Enc.Encode_Unsigned (2) & Enc.Encode_Array (2)
        & Enc.Encode_Unsigned (CBOR.UInt64 (Value.Cursor.Evidence_Sequence))
        & Enc.Encode_Unsigned (CBOR.UInt64 (Value.Cursor.Message_Ordinal))
        & Enc.Encode_Unsigned (3) & Enc.Encode_Byte_String (To_CBOR (Value.Evidence_Digest))
        & Enc.Encode_Unsigned (4) & Enc.Encode_Byte_String (To_CBOR (Value.Session_Id))
        & Enc.Encode_Unsigned (5) & Enc.Encode_Array (3)
        & Enc.Encode_Text_String_UTF8 (FID)
        & Enc.Encode_Unsigned (CBOR.UInt64 (Types.Galaxy_Realm'Pos (Value.Profile.Realm)))
        & Enc.Encode_Unsigned (CBOR.UInt64 (Value.Profile.Save_Epoch))
        & Enc.Encode_Unsigned (6)
        & Enc.Encode_Unsigned (CBOR.UInt64 (Types.Observation_Kind'Pos (Value.Kind) + 1))
        & Enc.Encode_Unsigned (7) & Encode_Source_Time (Value)
        & Enc.Encode_Unsigned (8) & Encode_Signed (Value.Observed_Unix_Ms)
        & Enc.Encode_Unsigned (9) & Encode_Signed (Value.Commit_Unix_Ms)
        & Enc.Encode_Unsigned (10) & Enc.Encode_Unsigned (CBOR.UInt64 (Value.Message_Count))
        & Enc.Encode_Unsigned (11) & Encode_Payload (Value)
        & Enc.Encode_Unsigned (12)
        & Enc.Encode_Unsigned (CBOR.UInt64 (Types.Source_Provenance'Pos (Value.Provenance)));
   end Encode_Observation;

   function Decode_Observation
     (Data : CBOR.Byte_Array) return Decode_Result is
      R : CBOR.Decode_All_Result;
      Result : Decode_Result;
      Index : Natural := 1;

      procedure Take (Item : out CBOR.CBOR_Item) is
      begin
         if Index > Natural (R.Count) then
            raise Schema_Error;
         end if;
         Item := R.Items (CBOR.Item_Range (Index));
         Index := Index + 1;
      end Take;

      procedure Read_U64 (Value : out Interfaces.Unsigned_64) is
         Item : CBOR.CBOR_Item;
      begin
         Take (Item);
         if Item.Kind /= CBOR.MT_Unsigned_Integer then
            raise Schema_Error;
         end if;
         Value := Interfaces.Unsigned_64 (Item.UInt_Value);
      end Read_U64;

      procedure Expect_U64 (Expected : Interfaces.Unsigned_64) is
         Value : Interfaces.Unsigned_64;
      begin
         Read_U64 (Value);
         if Value /= Expected then
            raise Schema_Error;
         end if;
      end Expect_U64;

      procedure Read_I64 (Value : out Interfaces.Integer_64) is
         Item : CBOR.CBOR_Item;
      begin
         Take (Item);
         case Item.Kind is
            when CBOR.MT_Unsigned_Integer =>
               if Item.UInt_Value > CBOR.UInt64 (Interfaces.Integer_64'Last) then
                  raise Schema_Error;
               end if;
               Value := Interfaces.Integer_64 (Item.UInt_Value);
            when CBOR.MT_Negative_Integer =>
               if Item.NInt_Arg > CBOR.UInt64 (Interfaces.Integer_64'Last) then
                  raise Schema_Error;
               end if;
               Value := -1 - Interfaces.Integer_64 (Item.NInt_Arg);
            when others =>
               raise Schema_Error;
         end case;
      end Read_I64;

      procedure Expect_Map (Count : CBOR.UInt64) is
         Item : CBOR.CBOR_Item;
      begin
         Take (Item);
         if Item.Kind /= CBOR.MT_Map or else Item.Map_Count /= Count then
            raise Schema_Error;
         end if;
      end Expect_Map;

      procedure Expect_Array (Count : CBOR.UInt64) is
         Item : CBOR.CBOR_Item;
      begin
         Take (Item);
         if Item.Kind /= CBOR.MT_Array or else Item.Arr_Count /= Count then
            raise Schema_Error;
         end if;
      end Expect_Array;

      procedure Expect_Null is
         Item : CBOR.CBOR_Item;
      begin
         Take (Item);
         if Item.Kind /= CBOR.MT_Simple_Value
           or else Item.SV_Value /= CBOR.Simple_Null
           or else Item.Float_Ref.Length /= 0
         then
            raise Schema_Error;
         end if;
      end Expect_Null;

      procedure Read_Bytes_32 (Value : out Types.Byte_32) is
         Item : CBOR.CBOR_Item;
      begin
         Take (Item);
         if Item.Kind /= CBOR.MT_Byte_String or else Item.BS_Ref.Length /= 32 then
            raise Schema_Error;
         end if;
         for I in Value'Range loop
            Value (I) := Interfaces.Unsigned_8
              (Data (Item.BS_Ref.First + CBOR.SE_Offset (I - 1)));
         end loop;
      end Read_Bytes_32;

      procedure Read_Bytes_16 (Value : out Types.Byte_16) is
         Item : CBOR.CBOR_Item;
      begin
         Take (Item);
         if Item.Kind /= CBOR.MT_Byte_String or else Item.BS_Ref.Length /= 16 then
            raise Schema_Error;
         end if;
         for I in Value'Range loop
            Value (I) := Interfaces.Unsigned_8
              (Data (Item.BS_Ref.First + CBOR.SE_Offset (I - 1)));
         end loop;
      end Read_Bytes_16;

      procedure Read_Text_64 (Value : out Types.Text_64) is
         Item : CBOR.CBOR_Item;
      begin
         Take (Item);
         if Item.Kind /= CBOR.MT_Text_String
           or else Item.TS_Ref.Length not in 1 .. 64
         then
            raise Schema_Error;
         end if;
         Value := (others => <>);
         Value.Length := Natural (Item.TS_Ref.Length);
         for I in 1 .. Value.Length loop
            Value.Data (I) := Character'Val
              (Integer (Data (Item.TS_Ref.First + CBOR.SE_Offset (I - 1))));
         end loop;
      end Read_Text_64;

      procedure Read_Text_128 (Value : out Types.Text_128) is
         Item : CBOR.CBOR_Item;
      begin
         Take (Item);
         if Item.Kind /= CBOR.MT_Text_String
           or else Item.TS_Ref.Length not in 1 .. 128
         then
            raise Schema_Error;
         end if;
         Value := (others => <>);
         Value.Length := Natural (Item.TS_Ref.Length);
         for I in 1 .. Value.Length loop
            Value.Data (I) := Character'Val
              (Integer (Data (Item.TS_Ref.First + CBOR.SE_Offset (I - 1))));
         end loop;
      end Read_Text_128;

      procedure Read_Decimal (Value : out Types.Decimal_64) is
         Coefficient : Interfaces.Integer_64;
         Exponent    : Interfaces.Integer_64;
      begin
         Expect_Array (2);
         Read_I64 (Coefficient);
         Read_I64 (Exponent);
         if Exponent not in -18 .. 18
           or else (Coefficient = 0 and then Exponent /= 0)
           or else (Coefficient /= 0 and then Coefficient mod 10 = 0)
         then
            raise Schema_Error;
         end if;
         Value := (Coefficient => Coefficient, Exponent => Integer (Exponent));
      end Read_Decimal;

      procedure Reject_Unsupported_Items is
      begin
         for J in CBOR.Item_Range range 1 .. R.Count loop
            declare
               Item : constant CBOR.CBOR_Item := R.Items (J);
            begin
               case Item.Kind is
                  when CBOR.MT_Tag =>
                     raise Schema_Error;
                  when CBOR.MT_Array =>
                     if Item.Arr_Count = CBOR.UInt64'Last then
                        raise Schema_Error;
                     end if;
                  when CBOR.MT_Map =>
                     if Item.Map_Count = CBOR.UInt64'Last then
                        raise Schema_Error;
                     end if;
                  when CBOR.MT_Simple_Value =>
                     if Item.Float_Ref.Length /= 0 or else Item.SV_Value = 31 then
                        raise Schema_Error;
                     end if;
                  when others =>
                     null;
               end case;
            end;
         end loop;
      end Reject_Unsupported_Items;

      procedure Read_Profile is
         Realm : Interfaces.Unsigned_64;
      begin
         Expect_Array (3);
         Read_Text_64 (Result.Observation.Profile.FID);
         Read_U64 (Realm);
         case Realm is
            when 0 => Result.Observation.Profile.Realm := Types.Unknown;
            when 1 => Result.Observation.Profile.Realm := Types.Live;
            when 2 => Result.Observation.Profile.Realm := Types.Legacy;
            when 3 => Result.Observation.Profile.Realm := Types.Beta_Or_PTS;
            when others => raise Schema_Error;
         end case;
         Read_U64 (Result.Observation.Profile.Save_Epoch);
      end Read_Profile;

      procedure Read_Source_Time is
         Item : CBOR.CBOR_Item;
      begin
         if Index > Natural (R.Count) then
            raise Schema_Error;
         end if;
         Item := R.Items (CBOR.Item_Range (Index));
         if Item.Kind = CBOR.MT_Simple_Value
           and then Item.SV_Value = CBOR.Simple_Null
           and then Item.Float_Ref.Length = 0
         then
            Index := Index + 1;
            Result.Observation.Source_Time_Present := False;
            Result.Observation.Source_Time_Unix_Ms := 0;
         else
            Result.Observation.Source_Time_Present := True;
            Read_I64 (Result.Observation.Source_Time_Unix_Ms);
         end if;
      end Read_Source_Time;

      procedure Read_FSD_Jump is
      begin
         Expect_Map (6);
         Expect_U64 (0);
         Read_Text_128 (Result.Observation.Jump.Star_System);
         Expect_U64 (1);
         Read_U64 (Result.Observation.Jump.System_Address);
         Expect_U64 (2);
         Expect_Array (3);
         Read_Decimal (Result.Observation.Jump.Position.X);
         Read_Decimal (Result.Observation.Jump.Position.Y);
         Read_Decimal (Result.Observation.Jump.Position.Z);
         Expect_U64 (3);
         Read_Decimal (Result.Observation.Jump.Jump_Distance);
         Expect_U64 (4);
         Read_Decimal (Result.Observation.Jump.Fuel_Used);
         Expect_U64 (5);
         Read_Decimal (Result.Observation.Jump.Fuel_Level);
      end Read_FSD_Jump;

      procedure Read_Kind is
         Kind : Interfaces.Unsigned_64;
      begin
         Read_U64 (Kind);
         case Kind is
            when 1 => Result.Observation.Kind := Types.Session_Bound;
            when 2 => Result.Observation.Kind := Types.FSD_Jump;
            when others => raise Schema_Error;
         end case;
      end Read_Kind;

      procedure Read_Message_Count is
         Count : Interfaces.Unsigned_64;
      begin
         Read_U64 (Count);
         if Count not in 1 .. Interfaces.Unsigned_64 (Interfaces.Unsigned_16'Last) then
            raise Schema_Error;
         end if;
         Result.Observation.Message_Count := Interfaces.Unsigned_16 (Count);
      end Read_Message_Count;

      procedure Read_Provenance is
         Value : Interfaces.Unsigned_64;
      begin
         Read_U64 (Value);
         case Value is
            when 0 => Result.Observation.Provenance := Types.Unknown_Source;
            when 1 => Result.Observation.Provenance := Types.Local_Journal;
            when 2 => Result.Observation.Provenance := Types.Local_Status;
            when 3 => Result.Observation.Provenance := Types.Frontier_API;
            when 4 => Result.Observation.Provenance := Types.Community;
            when 5 => Result.Observation.Provenance := Types.User_Entered;
            when others => raise Schema_Error;
         end case;
      end Read_Provenance;

   begin
      if Data'Length > Maximum_Payload_Bytes then
         Result.Status := Resource_Limit;
         return Result;
      end if;

      R := Dec.Decode_All_Strict
        (Data, Check_UTF8 => True, Max_String_Len => 128, Max_Depth => 4);

      if R.Status /= CBOR.OK then
         Result.Status := (if Is_Resource_Error (R.Status) then Resource_Limit else Invalid_CBOR);
         return Result;
      end if;
      begin
         Reject_Unsupported_Items;
         Expect_Map (13);
         Expect_U64 (0); Expect_U64 (2);
         Expect_U64 (1); Expect_U64 (1);
         Expect_U64 (2); Expect_Array (2);
         Read_U64 (Result.Observation.Cursor.Evidence_Sequence);
         declare
            Ordinal : Interfaces.Unsigned_64;
         begin
            Read_U64 (Ordinal);
            if Ordinal > Interfaces.Unsigned_64 (Interfaces.Unsigned_32'Last) then
               raise Schema_Error;
            end if;
            Result.Observation.Cursor.Message_Ordinal := Interfaces.Unsigned_32 (Ordinal);
         end;
         Expect_U64 (3); Read_Bytes_32 (Result.Observation.Evidence_Digest);
         Expect_U64 (4); Read_Bytes_16 (Result.Observation.Session_Id);
         Expect_U64 (5); Read_Profile;
         Expect_U64 (6); Read_Kind;
         Expect_U64 (7); Read_Source_Time;
         Expect_U64 (8); Read_I64 (Result.Observation.Observed_Unix_Ms);
         Expect_U64 (9); Read_I64 (Result.Observation.Commit_Unix_Ms);
         Expect_U64 (10); Read_Message_Count;
         Expect_U64 (11);
         case Result.Observation.Kind is
            when Types.Session_Bound => Expect_Null;
            when Types.FSD_Jump      => Read_FSD_Jump;
         end case;
         Expect_U64 (12); Read_Provenance;

         if Index /= Natural (R.Count) + 1 then
            raise Schema_Error;
         end if;

         Result.Status := OK;
         return Result;
      exception
         when Schema_Error | Constraint_Error =>
            Result.Status := Invalid_Schema;
            return Result;
      end;
   end Decode_Observation;

   function Decode_Host_Message
     (Data : CBOR.Byte_Array) return Host_Decode_Result
   is
      R      : CBOR.Decode_All_Result;
      Result : Host_Decode_Result;

      function Unsigned_At (Index : Positive) return CBOR.UInt64 is
         Item : CBOR.CBOR_Item;
      begin
         if Index > Natural (R.Count) then
            raise Schema_Error;
         end if;
         Item := R.Items (CBOR.Item_Range (Index));
         if Item.Kind /= CBOR.MT_Unsigned_Integer then
            raise Schema_Error;
         end if;
         return Item.UInt_Value;
      end Unsigned_At;
   begin
      if Data'Length > Maximum_Payload_Bytes then
         Result.Status := Resource_Limit;
         return Result;
      end if;

      R := Dec.Decode_All_Strict
        (Data, Check_UTF8 => True, Max_String_Len => 128, Max_Depth => 4);
      if R.Status /= CBOR.OK then
         Result.Status :=
           (if Is_Resource_Error (R.Status) then Resource_Limit else Invalid_CBOR);
         return Result;
      end if;
      begin
         if R.Count < 3
           or else R.Items (1).Kind /= CBOR.MT_Map
           or else Unsigned_At (2) /= 0
         then
            raise Schema_Error;
         end if;

         case Unsigned_At (3) is
            when 1 =>
               if R.Items (1).Map_Count /= 3
                 or else Natural (R.Count) /= 7
                 or else Unsigned_At (4) /= 1
                 or else Unsigned_At (6) /= 2
               then
                  raise Schema_Error;
               end if;
               Result.Kind := Set_Role_Message;
               Result.Epoch := Interfaces.Unsigned_64 (Unsigned_At (5));
               case Unsigned_At (7) is
                  when 0 => Result.Role := Wolpertinger_Control.Shadow;
                  when 1 => Result.Role := Wolpertinger_Control.Active;
                  when others => raise Schema_Error;
               end case;
               Result.Status := OK;

            when 2 =>
               declare
                  Observation_Result : constant Decode_Result := Decode_Observation (Data);
               begin
                  Result.Status := Observation_Result.Status;
                  Result.Kind := Apply_Observation_Message;
                  Result.Observation := Observation_Result.Observation;
               end;

            when others =>
               raise Schema_Error;
         end case;
         return Result;
      exception
         when Schema_Error | Constraint_Error =>
            Result.Status := Invalid_Schema;
            return Result;
      end;
   end Decode_Host_Message;

   function Encode_Cursor
     (Value : Types.Observation_Cursor) return CBOR.Byte_Array is
     (Enc.Encode_Array (2)
      & Enc.Encode_Unsigned (CBOR.UInt64 (Value.Evidence_Sequence))
      & Enc.Encode_Unsigned (CBOR.UInt64 (Value.Message_Ordinal)));

   function Encode_Jump_Fact
     (Value : Wolpertinger_Facts.Jump_Fact) return CBOR.Byte_Array
   is
      Star : constant CBOR.Byte_Array := To_CBOR (Value.Star_System);
   begin
      if Value.Star_System.Length not in 1 .. 128
        or else not Dec.Is_Valid_UTF8 (Star)
      then
         raise Constraint_Error with "invalid JumpFact StarSystem";
      end if;

      return Enc.Encode_Array (11)
        & Encode_Cursor (Value.Cursor)
        & Enc.Encode_Unsigned (CBOR.UInt64 (Value.System_Address))
        & Enc.Encode_Text_String_UTF8 (Star)
        & Enc.Encode_Array (3)
        & Encode_Decimal (Value.Position.X)
        & Encode_Decimal (Value.Position.Y)
        & Encode_Decimal (Value.Position.Z)
        & Encode_Decimal (Value.Jump_Distance)
        & Encode_Decimal (Value.Fuel_Used)
        & Encode_Decimal (Value.Fuel_Level)
        & Enc.Encode_Unsigned
            (CBOR.UInt64 (Types.Source_Provenance'Pos (Value.Location_Provenance)))
        & Enc.Encode_Unsigned
            (CBOR.UInt64
               (Wolpertinger_Facts.State_Types.Freshness_State'Pos
                  (Value.Location_Freshness)))
        & Enc.Encode_Unsigned
            (CBOR.UInt64 (Types.Source_Provenance'Pos (Value.Fuel_Provenance)))
        & Enc.Encode_Unsigned
            (CBOR.UInt64
               (Wolpertinger_Facts.State_Types.Freshness_State'Pos
                  (Value.Fuel_Freshness)));
   end Encode_Jump_Fact;

   function Encode_Response
     (Value : Kernel_Response) return CBOR.Byte_Array is
      Cursor_Value : constant CBOR.Byte_Array :=
        (if Value.Has_Cursor then Encode_Cursor (Value.Cursor) else Enc.Encode_Null);
      Jump_Value : constant CBOR.Byte_Array :=
        (if Value.Has_Jump_Fact then Encode_Jump_Fact (Value.Jump) else Enc.Encode_Null);
   begin
      return Enc.Encode_Map (6)
        & Enc.Encode_Unsigned (0)
        & Enc.Encode_Unsigned (CBOR.UInt64 (Kernel_Response_Kind'Pos (Value.Kind) + 1))
        & Enc.Encode_Unsigned (1)
        & Enc.Encode_Unsigned (CBOR.UInt64 (Kernel_Response_Status'Pos (Value.Status)))
        & Enc.Encode_Unsigned (2)
        & Enc.Encode_Unsigned (CBOR.UInt64 (Value.Epoch))
        & Enc.Encode_Unsigned (3)
        & Cursor_Value
        & Enc.Encode_Unsigned (4)
        & Enc.Encode_Byte_String (To_CBOR (Value.State_Digest))
        & Enc.Encode_Unsigned (5)
        & Jump_Value;
   end Encode_Response;

end Wolpertinger_Protocol;
