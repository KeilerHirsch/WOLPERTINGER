with CBOR;
with Wolpertinger_Types;

package Wolpertinger_Protocol is

   Maximum_Payload_Bytes : constant CBOR.SE_Offset := 65_536;

   type Decode_Status is
     (OK,
      Invalid_CBOR,
      Invalid_Schema,
      Resource_Limit);

   type Decode_Result is record
      Status      : Decode_Status := Invalid_CBOR;
      Observation : Wolpertinger_Types.Observation;
   end record;

   function Decode_Observation
     (Data : CBOR.Byte_Array)
      return Decode_Result;

   function Encode_Observation
     (Value : Wolpertinger_Types.Observation)
      return CBOR.Byte_Array;

end Wolpertinger_Protocol;
