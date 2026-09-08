with Wolpertinger_Types;

package Wolpertinger_Bounded_Text with SPARK_Mode is

   function To_Text_64 (Value : String) return Wolpertinger_Types.Text_64
     with Pre  => Value'Length <= 64,
          Post => To_Text_64'Result.Length = Value'Length;

   function To_Text_128 (Value : String) return Wolpertinger_Types.Text_128
     with Pre  => Value'Length <= 128,
          Post => To_Text_128'Result.Length = Value'Length;

   function Equal
     (Left, Right : Wolpertinger_Types.Text_64) return Boolean;

   function Equal
     (Left, Right : Wolpertinger_Types.Text_128) return Boolean;

end Wolpertinger_Bounded_Text;
