with Interfaces;

package Wolpertinger_Control with SPARK_Mode is
   use type Interfaces.Unsigned_64;
   type Kernel_Role is (Shadow, Active);

   type Control_State is record
      Current_Epoch : Interfaces.Unsigned_64 := 0;
      Role          : Kernel_Role := Shadow;
   end record;

   procedure Set_Role
     (State     : in out Control_State;
      New_Epoch : Interfaces.Unsigned_64;
      New_Role  : Kernel_Role;
      Accepted  : out Boolean)
     with Post =>
       (Accepted = (New_Epoch > State'Old.Current_Epoch))
       and then
       (if Accepted then
          State.Current_Epoch = New_Epoch and then State.Role = New_Role
        else State = State'Old);
end Wolpertinger_Control;
