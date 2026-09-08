with Ada.Command_Line;
with Interfaces;
with Interfaces.C_Streams;
with System.Storage_Elements;
with Wolpertinger_Control;
with Wolpertinger_Digest;
with Wolpertinger_Engine;
with Wolpertinger_Framing;
with Wolpertinger_Protocol;
with Wolpertinger_State;

procedure Wolpertinger_Kernel_Main is
   package CLI renames Ada.Command_Line;
   package CS renames Interfaces.C_Streams;
   package SSE renames System.Storage_Elements;
   package Control renames Wolpertinger_Control;
   package Digest renames Wolpertinger_Digest;
   package Engine renames Wolpertinger_Engine;
   package Framing renames Wolpertinger_Framing;
   package Protocol renames Wolpertinger_Protocol;
   package State_Types renames Wolpertinger_State;

   use type CS.size_t;
   use type Interfaces.Unsigned_32;
   use type SSE.Storage_Offset;
   use type Protocol.Decode_Status;

   Input_Stream  : constant CS.FILEs := CS.stdin;
   Output_Stream : constant CS.FILEs := CS.stdout;
   State         : State_Types.Kernel_State := (others => <>);
   Authority     : Control.Control_State := (others => <>);
   type Read_Status is (Read_OK, Clean_EOF, Read_Truncated);

   function Read_Exact
     (Stream : CS.FILEs; Data : out SSE.Storage_Array; Allow_EOF : Boolean)
      return Read_Status
   is
      Offset : CS.size_t := 0;
   begin
      while Offset < CS.size_t (Data'Length) loop
         declare
            Count : constant CS.size_t :=
              CS.fread
                (Data'Address,
                 Offset,
                 1,
                 CS.size_t (Data'Length) - Offset,
                 Stream);
         begin
            if Count = 0 then
               if Offset = 0 and then Allow_EOF then
                  return Clean_EOF;
               else
                  return Read_Truncated;
               end if;
            end if;
            Offset := Offset + Count;
         end;
      end loop;
      return Read_OK;
   end Read_Exact;
   function Decode_Length (Header : SSE.Storage_Array) return Interfaces.Unsigned_32 is
   begin
      return Interfaces.Unsigned_32 (Header (Header'First)) * 16#1000000#
        + Interfaces.Unsigned_32 (Header (Header'First + 1)) * 16#10000#
        + Interfaces.Unsigned_32 (Header (Header'First + 2)) * 16#100#
        + Interfaces.Unsigned_32 (Header (Header'First + 3));
   end Decode_Length;

   procedure Write_All (Stream : CS.FILEs; Data : SSE.Storage_Array) is
      Offset : CS.size_t := 0;
   begin
      while Offset < CS.size_t (Data'Length) loop
         declare
            Count : constant CS.size_t :=
              CS.fwrite
                (Data (Data'First + SSE.Storage_Offset (Offset))'Address,
                 1,
                 CS.size_t (Data'Length) - Offset,
                 Stream);
         begin
            if Count = 0 then
               raise Program_Error with "kernel stdout write failed";
            end if;
            Offset := Offset + Count;
         end;
      end loop;
   end Write_All;
   procedure Send_Response (Value : Protocol.Kernel_Response) is
      Payload : constant SSE.Storage_Array := Protocol.Encode_Response (Value);
      Frame   : constant SSE.Storage_Array := Framing.Encode_Frame (Payload);
   begin
      Write_All (Output_Stream, Frame);
      if CS.fflush (Output_Stream) /= 0 then
         raise Program_Error with "kernel stdout flush failed";
      end if;
   end Send_Response;

   function To_Response_Status
     (Status : Engine.Apply_Status) return Protocol.Kernel_Response_Status is
   begin
      case Status is
         when Engine.Applied            => return Protocol.Response_OK;
         when Engine.Idempotent         => return Protocol.Response_Idempotent;
         when Engine.Sequence_Gap       => return Protocol.Response_Sequence_Gap;
         when Engine.Integrity_Fault    => return Protocol.Response_Integrity_Fault;
         when Engine.Identity_Conflict  => return Protocol.Response_Identity_Conflict;
      end case;
   end To_Response_Status;

   procedure Process_Payload (Payload : SSE.Storage_Array) is
      Message  : constant Protocol.Host_Decode_Result := Protocol.Decode_Host_Message (Payload);
      Response : Protocol.Kernel_Response := (others => <>);
   begin
      Response.Epoch := Authority.Current_Epoch;
      Response.State_Digest := Digest.State_Digest (State);
      if Message.Status /= Protocol.OK then
         Response.Kind := Protocol.Apply_Response;
         Response.Status := Protocol.Response_Invalid_Message;
         Send_Response (Response);
         return;
      end if;

      case Message.Kind is
         when Protocol.Set_Role_Message =>
            declare
               Accepted : Boolean;
            begin
               Control.Set_Role
                 (Authority, Message.Epoch, Message.Role, Accepted);
               Response.Kind := Protocol.Role_Response;
               Response.Status :=
                 (if Accepted then Protocol.Response_OK
                  else Protocol.Response_Stale_Epoch);
               Response.Epoch := Authority.Current_Epoch;
               Response.State_Digest := Digest.State_Digest (State);
            end;

         when Protocol.Apply_Observation_Message =>
            declare
               Result : Engine.Apply_Result;
            begin
               Engine.Apply (State, Message.Observation, Result);
               Response.Kind := Protocol.Apply_Response;
               Response.Status := To_Response_Status (Result.Status);
               Response.Epoch := Authority.Current_Epoch;
               Response.Has_Cursor := True;
               Response.Cursor := Message.Observation.Cursor;
               Response.State_Digest := Digest.State_Digest (State);
               Response.Has_Jump_Fact := Result.Has_Jump_Fact;
               Response.Jump := Result.Jump;
            end;
      end case;

      Send_Response (Response);
   end Process_Payload;

begin
   CS.set_binary_mode (CS.fileno (Input_Stream));
   CS.set_binary_mode (CS.fileno (Output_Stream));

   loop
      declare
         Header : SSE.Storage_Array (1 .. 4);
         Header_Status : constant Read_Status :=
           Read_Exact (Input_Stream, Header, Allow_EOF => True);
      begin
         case Header_Status is
            when Clean_EOF =>
               exit;
            when Read_Truncated =>
               raise Program_Error with "truncated kernel frame header";
            when Read_OK =>
               null;
         end case;

         declare
            Length : constant Interfaces.Unsigned_32 := Decode_Length (Header);
         begin
            if Length = 0
              or else Length > Interfaces.Unsigned_32 (Framing.Maximum_Payload_Bytes)
            then
               raise Program_Error with "invalid kernel frame length";
            end if;

            declare
               Payload : SSE.Storage_Array (1 .. SSE.Storage_Offset (Length));
               Payload_Status : constant Read_Status :=
                 Read_Exact (Input_Stream, Payload, Allow_EOF => False);
            begin
               if Payload_Status /= Read_OK then
                  raise Program_Error with "truncated kernel frame payload";
               end if;
               Process_Payload (Payload);
            end;
         end;
      end;
   end loop;
exception
   when others =>
      CLI.Set_Exit_Status (CLI.Failure);
end Wolpertinger_Kernel_Main;
