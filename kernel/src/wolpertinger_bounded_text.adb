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
