<a id="top"></a>
# aqorde ScreenMap Maker
**Framing and composition guide · version 0.5.10b (beta)**
A practical guide from your first grid to a reference for AI image and video generation.

## Contents
- [Purpose](#purpose)
- [First five minutes](#start)
- [Main window and frame format](#window)
- [Perspective and radius](#perspective)
- [Depth D0–D7](#depth)
- [Zones, grid and levels](#zones)
- [Perspective Grid Field Area](#pgfa)
- [Primitive mode and centre marker](#primitive)
- [Background image](#background)
- [Appearance and floating windows](#appearance)
- [Quick menu and keyboard](#shortcuts)
- [Save and restore](#projects)
- [PNG and SVG export](#export)
- [Writing a useful prompt](#prompt)
- [Example: orcas beneath an iceberg](#orcas)
- [Troubleshooting and limitations](#faq)

<a id="purpose"></a>
## Purpose
ScreenMap Maker creates a **composition reference**: a map of screen zones, depth planes and vertical bands. You can assign each subject an address instead of repeatedly asking for it to be “a little lower, farther away and slightly to the right”.
Use it for image prompts, storyboards, video keyframes, scene blocking and overlays on existing sketches or photographs. It exports a grid, optionally with a background photograph. Image generation and 3D rendering take place in other applications.
> Grid coordinates are a convention between you and the generative model. Attach the map and explain its legend. The model's ability to follow exact positions still matters.
[↑ Back to top](#top)

<a id="start"></a>
## First five minutes
1. Choose **Edit → Aspect ratio**, then **Orientation**.
2. Leave **Edit → Plane → Perspective** selected.
3. Enable **Move perspective** above the viewport. Drag inside the frame to place its centre.
4. Drag the blue handle beside the circle to adjust its radius.
5. Use the eyes in the sidebar to show only the layers you need. Start with Grid, Zones and Depth.
6. Turn off a layer's **Caption** checkbox if its text obstructs the view.
7. Open **File → Settings → Appearance** to choose colours and line widths.
8. Save a `.screenmap` project, then export PNG or SVG.
9. Attach the export to your generation prompt. Explain: “D0 is nearest; D7 is farthest; numbers identify screen regions. Remove grid lines and labels from the final artwork.”
For a flat layout, choose **Primitive** instead. It uses numbered rows and columns without moving the vanishing point.
[↑ Back to top](#top)

<a id="window"></a>
## Main window and frame format
| Area | Purpose |
| --- | --- |
| File | New, Save, Open, Export, Settings |
| Edit | Undo, Redo, plane, aspect ratio and orientation |
| Help | This guide and About |
| Viewport | The frame preview; surrounding empty space is not exported |
| Quick menu | Your selected actions above the frame |
| Sidebar | Layers, geometry controls and background image |

| Family | Landscape | Portrait |
| --- | --- | --- |
| 16:9 | 16:9 | 9:16 |
| 3:4 | 4:3 | 3:4 |
| 1:1 | 1:1 | 1:1 |

“3:4” names a family; Landscape gives 4:3. Pixel dimensions are chosen during export. The viewport fits the frame without changing its aspect ratio. All D rectangles retain that ratio, becoming square only in 1:1.
Hover over a layer title for an explanation. The mouse wheel adjusts a hovered slider instead of scrolling the sidebar. Move the pointer away from the slider to scroll the panel.
[↑ Back to top](#top)

<a id="perspective"></a>
## Perspective and radius
Central zone **1** is the vanishing point. The inner and outer sectors, vertical levels, depth frames and PGFA adapt to its position.
### Move the centre
Enable **Move perspective**, press inside the frame and drag with the left mouse button. Drag past either side or use **Perspective X** to move the point outside the frame along the horizon. The edge arrow is draggable. Use **Perspective Reset** to centre the point. Release to finish a drag.
**Escape** cancels the active drag. **Ctrl+Z** after releasing the mouse undoes the entire gesture.
### Adjust the radius
Use the blue handle to the right of the circle or the Radius slider. The circle stays round for every aspect ratio. Radius is measured relative to the short frame side and drives the size of D7 and the other guide geometry.
D7 encloses the circle with a small gap while retaining the frame ratio. It need not fit the circle tightly on both axes. Near the frame edge, the allowed radius is limited so D7 remains inside D0. Move the centre inward if more room is needed.
> The centre marker represents composition and convergence. Its radius is not a lens focal length, aperture or depth-of-field setting.
[↑ Back to top](#top)

<a id="depth"></a>
## Depth D0–D7
| Planes | Suggested interpretation |
| --- | --- |
| D0–D1 | Foreground, close to the camera |
| D2–D3 | Near scene |
| D4–D5 | Middle and farther scene |
| D6–D7 | Distant background |

This is an **ordinal depth guide**, not a metre scale. You may define distances in your prompt, but the application does not calculate them.
Any screen zone can combine with any D. A subject can be at the lower left of the image and still be on D7. It does not have to sit directly on a red rectangle.
### Depth shift
Open **Settings → Appearance → Depth settings** below the layer table. Both Depth shift and Bound depth planes are located here.
- On: successive D labels decrease by 2 reference pixels; frame spacing accounts for the label sizes.
- Off: labels use the same requested size, and depth lines are masked behind the visible centre circle.
- In narrow bands, labels are additionally reduced to fit.
### Bound depth planes
- On: closed nested rectangles with connecting diagonals.
- Off: depth loses its vertical edges and PGFA shows the upper and lower planes with solid horizontals extending to both image edges. PGFA diagonals also fill the open areas to either side of the original planes. Moving the perspective centre updates the horizontals and diagonals. The corner rays stay solid, and dotted continuations extend the depth horizontals to the frame edges.
The viewport, PNG and SVG use the same depth order and guide geometry. Pin **Bound depth planes** under **Settings → Quick menu** for direct access above the viewport.
Depth captions automatically avoid zone numbers and other captions while remaining in their depth bands. Zone IDs retain their native positions. Crowded depth captions may shrink or use a free callout with a leader. Viewport, PNG and SVG share this layout.
[↑ Back to top](#top)

<a id="zones"></a>
## Zones, grid and levels
Perspective zone addresses are **1**, **1.1–1.8** and **2.1–2.24**. Outer IDs run clockwise with no gap. The original drawing skipped 2.8: old 2.9–2.25 map to new 2.8–2.24.
| Layer | Controls |
| --- | --- |
| Grid | Boundary lines and the central outline |
| Zones | Numbers inside the regions |
| Levels | TOP, MIDTOP, MIDBOTTOM and BOTTOM bands |

Grid and Zones have independent eyes and opacity. You can show lines without numbers or numbers without lines. Captions provide an additional text toggle. In Primitive mode, cell numbering runs left to right, top to bottom, from 1 to the total cell count.
Levels refer to **screen height**, not physical altitude or depth underwater. Their boundaries follow the perspective centre and radius. Specify a horizon separately in your prompt if one is needed.
[↑ Back to top](#top)

<a id="pgfa"></a>
## Perspective Grid Field Area
**PGFA** means Perspective Grid Field Area. It is a virtual guide box with floor, ceiling, side walls and a rear plane. The Russian interface calls it **ОППС**.
### Display just the box
1. Set the frame, vanishing point and radius in Perspective mode.
2. Disable Depth, Levels, Zones and Grid.
3. Enable PGFA.
4. Start with **PGFA density = 1** for sparse, readable guides.
5. Increase density to 2–6 for subdivisions.
6. Choose a subtle line colour and opacity.
Density adds intermediate lines while preserving the base D0–D7 frames. You may re-enable Depth to show plane captions above the field.
> These are manual composition guides. The field cells are not calibrated squares in world units.
[↑ Back to top](#top)

<a id="primitive"></a>
## Primitive mode and centre marker
Choose **Edit → Plane → Primitive** for posters, flat images, icons and simple layouts. The centre remains fixed, so Move perspective is disabled.
The Density slider has discrete detents. **Every step changes the cell count**; it does not travel through redundant percentage values. The mouse wheel advances one step.
| Frame | Minimum | Maximum |
| --- | --- | --- |
| 16:9 | 1×1 | 16×9 |
| 9:16 | 1×1 | 9×16 |
| 4:3 | 1×1 | 16×12 |
| 3:4 | 1×1 | 12×16 |
| 1:1 | 1×1 | 16×16 |

The long side allows up to 16 divisions. Intermediate cells may be rectangular, allowing 1×1 on a rectangular frame. Density is retained separately for 16:9, 3:4 and 1:1 families. Switching orientation swaps rows and columns at the same density step.
### Centre options
- **Centre point** enables the marker; Grid must also be visible.
- **Size** adjusts its radius independently from Perspective radius.
- **Hollow circle** replaces the filled dot with an outline. Lines inside retain 50% of their original opacity; cell numbers remain fully visible.
While Centre point is enabled, depth frames follow its size. If disabled, changing the hidden marker size leaves the depth unchanged. A hollow centre remains transparent when exported; it is not a background-coloured patch.
[↑ Back to top](#top)

<a id="background"></a>
## Background image
Set a solid colour through **Settings → Appearance → Background colour**. For example `#464646` gives RGB 70/70/70.
Choose **Picture…** in the sidebar to load a photograph. **Fill** covers the frame and crops excess image; **Fit** shows the complete image with free space where necessary.
Picture opacity blends the photo over the solid colour: 0 shows only that colour; 100% shows the photo fully. Crop X/Y chooses which part remains visible. Cropping only has a visible effect on an axis where the scaled photo extends beyond the frame.
Without an image, photo controls are disabled. Remove clears the current photo and can be undone. Input is limited to 64 MB and 64 million pixels.
The photo is embedded in the saved project, so the original file is not needed when you transfer it.
[↑ Back to top](#top)

<a id="appearance"></a>
## Appearance and floating windows
All settings apply immediately.
### Interface theme
Light uses the supplied Arctic palette; Dark uses the Rainy Twilight palette. System follows Windows. **Compact view** reduces spacing and control heights without reducing your chosen base font size.
The interface theme and exported map colours are separate settings.
### Layer display and style
Choose Icons, Text, or Icons and text for sidebar layer titles and pinned layer buttons. Tooltips remain available in icon-only mode.
| Setting | Meaning |
| --- | --- |
| Line width, px | Stroke width, 0.25–12 |
| Font size | Caption size, 8–72 |
| Colour | Layer line and label colour |
| Depth shift | Progressive D caption sizing and centre masking |
| Bound depth planes | Closed frames or open sides for sky and ground |

Map sizes use a **720 px short-side reference**. A 2 px stroke becomes about 4 output pixels on a frame whose short side is 1440 px. The same scaling applies to captions.
Zones contains numbers; change Grid line width to adjust their boundaries. The Grid caption in Primitive mode is the grid dimensions, not the cell numbers.
### Saved map themes
Select an existing theme using the arrow, or enter a custom name and press OK. Saving an existing custom name updates it. Built-in Dark and Light cannot be overwritten. Map themes include the solid background and layer styles.
### Fonts
Choose an installed font family; Calibri is the default. Base size defaults to 14 and controls interface text. Map caption sizes are independent.
### Move and resize a window
Drag the free part of a floating window's top header to move it. Settings, Export and Documentation can be resized by their edges. Palette, About and message windows fit their contents and have no manual resize. Long messages remain scrollable. Close with ×. Changes in Settings are already applied when it closes.
The colour palette has a hue strip and saturation/brightness square. Enter a complete `#RRGGBB` code for an exact colour; RGB values are shown below. Changes apply immediately. Close the palette and use Ctrl+Z in the main window to undo document colour changes.
[↑ Back to top](#top)

<a id="shortcuts"></a>
## Quick menu and keyboard
In **Settings → Quick menu**, check actions to pin them above the frame. The checkboxes are to the right. Colours and fonts stay in Appearance.
| Shortcut | Action |
| --- | --- |
| Ctrl+N | New document |
| Ctrl+O | Open project |
| Ctrl+S | Save project |
| Ctrl+E | Export |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Escape during drag | Cancel current gesture |

In a text field, Ctrl+Z/Y operates on its text. Interface preferences such as language, theme and Quick menu are outside document Undo history.
[↑ Back to top](#top)

<a id="projects"></a>
## Save and restore
**File → Save** writes a `.screenmap` project containing geometry, layer settings, export options and the embedded photo. The first save suggests `scene_yyyyMMdd_HHmmss.screenmap`, using the computer's local date and time. Keep it or enter your own name; later saves update the chosen file.
**Open** loads a project; **New** replaces the document with a fresh map while retaining interface preferences.
> Save important work before New or Open. This version has no unsaved-changes confirmation.
Closing the main window normally saves a session in `config` beside the EXE. The next launch restores the document, photo and preferences. This is not continuous crash recovery: save separate project files for important variations.
The portable directory must be writable. To move a whole session from v0.4, close both versions and copy its `config` folder into the new version. A release ZIP does not contain user configuration.
Projects can be transferred without the original photo. Fonts should be installed on the receiving computer; otherwise a substitute may be used. SVG exports avoid that dependency by storing glyph outlines.
[↑ Back to top](#top)

<a id="export"></a>
## PNG and SVG export
1. Choose **File → Export** and select PNG or SVG.
2. Enter the width. Height follows the aspect ratio; width is rounded to a compatible whole-pixel multiple.
3. Set PNG DPI if needed.
4. Choose whether to include the background picture.
5. Click Export and select a destination.
**PNG** is raster with alpha transparency. Limits are 8192 px per side and 64 million pixels total. DPI is print metadata: changing 96 to 300 DPI does not create extra image detail at the same pixel dimensions.
**SVG** contains vector lines and glyph outlines in named groups. An included photo is embedded in the file.
### Background picture checkbox
- Unchecked: export visible guides on transparency; the solid background is also excluded.
- Checked: include the photograph at its selected opacity plus the solid colour beneath it.
- No photo: the checkbox is disabled.
The export includes current visible layers, excluding application controls and blue editing handles. To export only PGFA, leave only that layer enabled.
[↑ Back to top](#top)

Enable **Export with prompt template** and choose **Русский / English** to its right: a matching Markdown file is saved beside the PNG/SVG. Its language is independent of the interface. It includes a current-map legend and editable fields for the scene, objects, lights and motion; fill the square-bracket placeholders before sending it to a model. Re-export preserves a differing old MD as .md.previous-….bak. Depth frames are not containers: objects may extend beyond their outlines at any screen position.

<a id="prompt"></a>
## Writing a useful prompt
Start with a legend, then assign each object a screen position, depth plane, visible size, pose and direction. Explicitly identify overlaps.
```
Use the attached screenmap as a 16:9 composition reference.
Numbers are screen zones. D0 is nearest and D7 is farthest.
Any screen zone can combine with any depth plane.
Do not draw the guides or labels in the final image.
```
A 3:2 area ratio between two zones should mean **60% and 40% of the subject's visible silhouette**, not 60% of the entire frame. Explain that convention.
For video, separate beginning, middle and end. D5 → D3 → D1 describes approaching the camera; specify speed separately. For a still image, describe one instant instead of merging multiple positions.
For each light source, specify direction, position/depth, colour, softness and the subject parts it illuminates. The application does not trace light rays or calculate shadows.
If the model confuses coordinates, simplify the map and repeat important positions in plain words such as “lower right” and “distant background”.
[↑ Back to top](#top)

<a id="orcas"></a>
## Example: orcas beneath an iceberg
Use a landscape 16:9 perspective map, with the centre slightly above the middle to leave more room below for the male's roll.
```
Arctic water, camera completely submerged and looking forward.
TOP contains the underside of the rippled water surface,
fading towards the TOP / MIDTOP boundary.

The submerged iceberg occupies zones 1.2 and 1.3 in a 3:2
silhouette-area ratio, on D6–D7. It is physically huge but distant,
with reduced contrast and details obscured by water.

The female occupies 1.4 and 1.5 in a 3:1 ratio, on D4.
She swims towards the male and slightly towards the camera.

The male spans 2.22, 1.7, 1.6, 2.17 and 2.18, on D2,
arching into a graceful underwater roll with his mouth slightly open.
He is nearer and overlaps the female where their silhouettes cross.

Soft cold illumination comes from the water surface above.
Remove all map lines and labels from the final image.
```
For video, add separate keyframes: female D5 → D4; both moving towards D2–D3; male beginning and completing the roll. **Distance from the camera** is separate from **metres below the water surface**.
[↑ Back to top](#top)

<a id="faq"></a>
## Troubleshooting and limitations
### Lines or numbers are missing
Check the layer eye, opacity, colour and Caption option. Boundary lines belong to Grid; cell and region numbers belong to Zones.
### The centre does not move
Choose Perspective, enable Move perspective, and drag inside the frame rather than its surrounding margins. The action is disabled in Primitive mode.
### D7 has an unexpected size
Change Perspective radius, or Centre point Size in Primitive mode with the point enabled. D7 preserves the frame aspect ratio and may be wider than the circle.
### A Zones line-width change does nothing
Zones has numbers only. Change line width in Grid instead.
### The sidebar does not scroll
Move the pointer away from a slider: a hovered slider intentionally consumes wheel input.
### The export has no photograph
Load a photo and check Export with background picture. Transparent exports exclude both photo and solid underlay.
### The session was not restored
Close the main window normally and ensure its `config` directory is writable. Save a project separately if a configuration error is reported.
### What is not included yet?
The manual grid is not a metric depth map or calibrated lens model. A separate Camera mode is planned after v0.5; fisheye requires a curved projection. 3D primitives, light sources, alternate scene views and object locking are future work.
The guide is compiled into the application. A neighbouring Markdown file cannot replace it; updating it requires a new build.
[↑ Back to top](#top)

## License
MIT License. © 2026 Alexander Andreev (aqorde). The license permits commercial use, modification and redistribution with the copyright and permission notices. The full text is in LICENSE.txt. Third-party components retain their own licenses.
