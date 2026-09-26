package body Wolpertinger_Bounded_Text with SPARK_Mode is

   function To_Text_64 (Value : String) return Wolpertinger_Types.Text_64 is
      Result : Wolpertinger_Types.Text_64;
   begin
      Result.Length := Value'Length;
      for Offset in 0 .. Value'Length - 1 loop
         Result.Data (Offset + 1) := Value (Value'First + Offset);
      end loop;
      return Result;
   end To_Text_64;

   function To_Text_128 (Value : String) return Wolpertinger_Types.Text_128 is
      Result : Wolpertinger_Types.Text_128;
   begin
      Result.Length := Value'Length;
      for Offset in 0 .. Value'Length - 1 loop
         Result.Data (Offset + 1) := Value (Value'First + Offset);
      end loop;
      return Result;
   end To_Text_128;

   function Is_Valid_UTF8
     (Value : Wolpertinger_Types.Text_128) return Boolean
   is
      I : Positive := 1;

      function Byte_At (Index : Positive) return Natural
         with Pre => Index <= Value.Data'Last
      is
      begin
         return Character'Pos (Value.Data (Index));
      end Byte_At;

      function Byte_In_Range
        (Index : Positive; Low, High : Natural) return Boolean is
        (Index <= Value.Length
         and then Byte_At (Index) in Low .. High);

      function Is_Continuation (Index : Positive) return Boolean is
        (Byte_In_Range (Index, 16#80#, 16#BF#));
   begin
      if Value.Length not in 1 .. 128 then
         return False;
      end if;

      while I <= Value.Length loop
         pragma Loop_Variant (Decreases => Value.Length - (I - 1));
         declare
            Lead : constant Natural := Byte_At (I);
         begin
            if Lead <= 16#7F# then
               I := I + 1;
            elsif Lead in 16#C2# .. 16#DF# then
               if not Is_Continuation (I + 1) then
                  return False;
               end if;
               I := I + 2;
            elsif Lead = 16#E0# then
               if not Byte_In_Range (I + 1, 16#A0#, 16#BF#)
                 or else not Is_Continuation (I + 2)
               then
                  return False;
               end if;
               I := I + 3;
            elsif Lead in 16#E1# .. 16#EC# or else Lead in 16#EE# .. 16#EF# then
               if not Is_Continuation (I + 1)
                 or else not Is_Continuation (I + 2)
               then
                  return False;
               end if;
               I := I + 3;
            elsif Lead = 16#ED# then
               if not Byte_In_Range (I + 1, 16#80#, 16#9F#)
                 or else not Is_Continuation (I + 2)
               then
                  return False;
               end if;
               I := I + 3;
            elsif Lead = 16#F0# then
               if not Byte_In_Range (I + 1, 16#90#, 16#BF#)
                 or else not Is_Continuation (I + 2)
                 or else not Is_Continuation (I + 3)
               then
                  return False;
               end if;
               I := I + 4;
            elsif Lead in 16#F1# .. 16#F3# then
               if not Is_Continuation (I + 1)
                 or else not Is_Continuation (I + 2)
                 or else not Is_Continuation (I + 3)
               then
                  return False;
               end if;
               I := I + 4;
            elsif Lead = 16#F4# then
               if not Byte_In_Range (I + 1, 16#80#, 16#8F#)
                 or else not Is_Continuation (I + 2)
                 or else not Is_Continuation (I + 3)
               then
                  return False;
               end if;
               I := I + 4;
            else
               return False;
            end if;
         end;
      end loop;

      return True;
   end Is_Valid_UTF8;
   function Equal
     (Left, Right : Wolpertinger_Types.Text_64) return Boolean is
   begin
      if Left.Length /= Right.Length then
         return False;
      end if;

      for I in 1 .. Left.Length loop
         if Left.Data (I) /= Right.Data (I) then
            return False;
         end if;
      end loop;
      return True;
   end Equal;

   function Equal
     (Left, Right : Wolpertinger_Types.Text_128) return Boolean is
   begin
      if Left.Length /= Right.Length then
         return False;
      end if;

      for I in 1 .. Left.Length loop
         if Left.Data (I) /= Right.Data (I) then
            return False;
         end if;
      end loop;
      return True;
   end Equal;

end Wolpertinger_Bounded_Text;
