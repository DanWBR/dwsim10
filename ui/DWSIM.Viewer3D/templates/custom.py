# Build profile of the DWSIM 3D export templates (Godot 4.7.2).
#
# The window draws a 3D scene with GDScript, Label3D text, particles, synthesized and Ogg sounds
# and imported glTF scenes; it uses no physics bodies, navigation, XR, networking beyond a local
# TCP socket, video, or runtime glTF/texture compression. So those parts are left out, and the
# binary is optimized for size with full link-time optimization.
#
#   scons platform=<windows|linuxbsd|macos> target=template_release arch=<arch> profile=custom.py

optimize = "size"
lto = "full"
production = "yes"
deprecated = "no"

# Rendering: Vulkan (Forward+) with the OpenGL 3 fallback; Metal on Apple Silicon is added by the
# macOS build line. Direct3D 12 stays out, so Windows runs on Vulkan.
d3d12 = "no"  # also given on the command line: the platform adds this option after the profile is read
accesskit = "no"

disable_physics_2d = "yes"
disable_physics_3d = "yes"
disable_navigation_2d = "yes"
disable_navigation_3d = "yes"
disable_xr = "yes"

# Modules the window does not use (SCons reads plain assignments only, so one line each).
module_astcenc_enabled = "no"
module_basis_universal_enabled = "no"
module_bcdec_enabled = "no"
module_betsy_enabled = "no"
module_bmp_enabled = "no"
module_camera_enabled = "no"
module_csg_enabled = "no"
module_cvtt_enabled = "no"
module_dds_enabled = "no"
module_enet_enabled = "no"
module_etcpak_enabled = "no"
module_fbx_enabled = "no"
module_gltf_enabled = "no"
module_godot_physics_2d_enabled = "no"
module_godot_physics_3d_enabled = "no"
module_gridmap_enabled = "no"
module_hdr_enabled = "no"
module_interactive_music_enabled = "no"
module_jolt_physics_enabled = "no"
module_jsonrpc_enabled = "no"
module_ktx_enabled = "no"
module_lightmapper_rd_enabled = "no"
module_mbedtls_enabled = "no"
module_meshoptimizer_enabled = "no"
module_mobile_vr_enabled = "no"
module_mono_enabled = "no"
module_mp3_enabled = "no"
module_multiplayer_enabled = "no"
module_navigation_2d_enabled = "no"
module_navigation_3d_enabled = "no"
module_noise_enabled = "no"
module_objectdb_profiler_enabled = "no"
module_openxr_enabled = "no"
module_raycast_enabled = "no"
module_regex_enabled = "no"
module_text_server_fb_enabled = "no"
module_tga_enabled = "no"
module_theora_enabled = "no"
module_tinyexr_enabled = "no"
module_upnp_enabled = "no"
module_vhacd_enabled = "no"
module_visual_shader_enabled = "no"
module_webrtc_enabled = "no"
module_websocket_enabled = "no"
module_webxr_enabled = "no"
module_xatlas_unwrap_enabled = "no"
module_zip_enabled = "no"
