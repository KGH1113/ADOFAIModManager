from pathlib import Path

app = str(Path(defines["app"]).resolve())
background_image = str(Path(defines["background"]).resolve())

files = [app]
symlinks = {"Applications": "/Applications"}
icon_locations = {
    "ADOFAI Mod Manager.app": (170, 200),
    "Applications": (490, 200),
}

format = "UDZO"
filesystem = "HFS+"
background = background_image
window_rect = ((120, 120), (660, 400))
default_view = "icon-view"
show_toolbar = False
show_status_bar = False
show_pathbar = False
show_sidebar = False
show_tab_view = False
show_icon_preview = True
include_icon_view_settings = True
arrange_by = None
label_pos = "bottom"
text_size = 13
icon_size = 112
hide_extensions = ["ADOFAI Mod Manager.app"]
