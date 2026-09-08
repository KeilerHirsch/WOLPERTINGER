with Wolpertinger_Bounded_Text;

package body Wolpertinger_State with SPARK_Mode is

   package Text renames Wolpertinger_Bounded_Text;

   use type Interfaces.Unsigned_8;
   use type Interfaces.Unsigned_16;
   use type Interfaces.Unsigned_32;
   use type Interfaces.Unsigned_64;
   use type Types.Galaxy_Realm;

   function Same_Profile
     (Left, Right : Types.Profile_Key) return Boolean is
     (Left.Realm = Right.Realm
      and then Left.Save_Epoch = Right.Save_Epoch
      and then Text.Equal (Left.FID, Right.FID));

   function Same_Cursor
     (Left, Right : Types.Observation_Cursor) return Boolean is
     (Left.Evidence_Sequence = Right.Evidence_Sequence
      and then Left.Message_Ordinal = Right.Message_Ordinal);

   function Same_Digest
     (Left, Right : Types.Byte_32) return Boolean is
   begin
      for I in Left'Range loop
         if Left (I) /= Right (I) then
            return False;
         end if;
      end loop;
      return True;
   end Same_Digest;
   function Cursor_Is_Valid
     (Observation : Types.Observation) return Boolean is
   begin
      return Observation.Message_Count > 0
        and then Observation.Cursor.Message_Ordinal
          < Interfaces.Unsigned_32 (Observation.Message_Count);
   end Cursor_Is_Valid;

   function Is_Next_Cursor
     (State : Kernel_State;
      Observation : Types.Observation) return Boolean is
   begin
      if not Cursor_Is_Valid (Observation) then
         return False;
      end if;

      if not State.Has_Last_Cursor then
         return Observation.Cursor.Evidence_Sequence = 1
           and then Observation.Cursor.Message_Ordinal = 0;
      end if;

      if Same_Cursor (State.Last_Cursor, Observation.Cursor) then
         return False;
      end if;

      if State.Last_Message_Count > 0
        and then State.Last_Cursor.Message_Ordinal
          < Interfaces.Unsigned_32 (State.Last_Message_Count - 1)
      then
         return Observation.Cursor.Evidence_Sequence = State.Last_Cursor.Evidence_Sequence
           and then Observation.Cursor.Message_Ordinal = State.Last_Cursor.Message_Ordinal + 1
           and then Observation.Message_Count = State.Last_Message_Count;
      end if;
      if State.Last_Cursor.Evidence_Sequence = Interfaces.Unsigned_64'Last then
         return False;
      end if;

      return Observation.Cursor.Evidence_Sequence = State.Last_Cursor.Evidence_Sequence + 1
        and then Observation.Cursor.Message_Ordinal = 0;
   end Is_Next_Cursor;

   function Identity_Matches
     (State : Kernel_State;
      Observation : Types.Observation) return Boolean is
   begin
      return State.Bound
        and then State.Session_Id = Observation.Session_Id
        and then Same_Profile (State.Profile, Observation.Profile);
   end Identity_Matches;

end Wolpertinger_State;
