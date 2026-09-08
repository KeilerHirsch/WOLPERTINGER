with CBOR;
with Interfaces;
with Wolpertinger_Control;
with Wolpertinger_Facts;
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

   type Host_Message_Kind is (Set_Role_Message, Apply_Observation_Message);

   type Host_Decode_Result is record
      Status      : Decode_Status := Invalid_CBOR;
      Kind        : Host_Message_Kind := Apply_Observation_Message;
      Epoch       : Interfaces.Unsigned_64 := 0;
      Role        : Wolpertinger_Control.Kernel_Role := Wolpertinger_Control.Shadow;
      Observation : Wolpertinger_Types.Observation;
   end record;

   type Kernel_Response_Kind is (Role_Response, Apply_Response);
   type Kernel_Response_Status is
     (Response_OK,
      Response_Idempotent,
      Response_Sequence_Gap,
      Response_Integrity_Fault,
      Response_Identity_Conflict,
      Response_Invalid_Message,
      Response_Stale_Epoch);

   type Kernel_Response is record
      Kind          : Kernel_Response_Kind := Apply_Response;
      Status        : Kernel_Response_Status := Response_Invalid_Message;
      Epoch         : Interfaces.Unsigned_64 := 0;
      Role          : Wolpertinger_Control.Kernel_Role := Wolpertinger_Control.Shadow;
      Has_Cursor    : Boolean := False;
      Cursor        : Wolpertinger_Types.Observation_Cursor;
      State_Digest  : Wolpertinger_Types.Byte_32 := [others => 0];
      Has_Jump_Fact : Boolean := False;
      Jump          : Wolpertinger_Facts.Jump_Fact;
   end record;

   function Decode_Observation
     (Data : CBOR.Byte_Array)
      return Decode_Result;

   function Encode_Observation
     (Value : Wolpertinger_Types.Observation)
      return CBOR.Byte_Array;

   function Decode_Host_Message
     (Data : CBOR.Byte_Array)
      return Host_Decode_Result;

   function Encode_Response
     (Value : Kernel_Response)
      return CBOR.Byte_Array;

end Wolpertinger_Protocol;
