Imports Ionic.Zip
Module modGlobals

    Public _STARTED As Boolean = False
    Public coffee_list As Integer
    Public model_name As String = ""
    Public Model_Loaded As Boolean
    '=========================================================
    Public x_max As Single = -10000
    Public x_min As Single = 10000
    Public y_min As Single = -10000
    Public y_max As Single = 10000
    Public z_max As Single = -10000
    Public z_min As Single = 10000
    Public bounding_size As Single
    Public checkerboard_id As Integer
    Public cur_texture_name As String
    Public file_name As String
    Public mouse_down As Boolean
    Public mouse_delta As New Point
    Public mouse_pos As New Point
    Public pb2_has_focus As Boolean
    Public rect_location As New Point
    Public old_h, old_w As Integer
    Public Zoom_Factor As Single = 1.0
    Public rect_size As New Point
    Public current_image As Integer
    Public center As vect2
    Public Structure vect2
        Public x, y As Single
    End Structure

    '=========================================================

    Public Structure vect3
        Public x, y, z As Single
    End Structure
    Public Structure vect4
        Public x, y, z, w As Single
    End Structure
    '=========================================================
    Public current_package As New ZipFile

    'Mouse view update screen variables
    Public mouse As New Point
    Public M_DOWN As Boolean = False
    Public move_cam_z As Boolean = False
    Public move_mod As Boolean = False
    Public U_Cam_X_angle, U_Cam_Y_angle, Cam_X_angle, Cam_Y_angle As Single
    Public look_point_x, look_point_y, look_point_z As Single
    Public U_look_point_x, U_look_point_y, U_look_point_z As Single
    Public angle_offset, u_View_Radius As Single
    Public view_radius As Single
    Public cam_x, cam_y, cam_z As Single
    Public eyeX, eyeY, eyeZ As Single
    Public z_move As Boolean = False
    Public gl_busy As Boolean = False
    Public screen_avg_counter, screen_avg_draw_time, screen_draw_time, screen_totaled_draw_time As Double
    Public stopGL As Boolean = False
    '=========================================================
    Public Temp_Storage As String = ""
    Public game_path As String = ""
End Module
