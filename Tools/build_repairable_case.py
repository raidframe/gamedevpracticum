"""Run with Blender --background --factory-startup --python this_file.
Builds a separate repairable asset; never overwrites the source case.
"""
import bpy, bmesh, json, hashlib
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Assets/models/Pc case/nfsmwd/PC_PSX.blend'
OUT = ROOT / 'Assets/models/Pc case/repairable'
ART = ROOT / 'ArtSource/PC_PSX_Repairable'
OUT.mkdir(parents=True, exist_ok=True)
ART.mkdir(parents=True, exist_ok=True)
before = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1
source = next(o for o in bpy.data.objects if o.type == 'MESH')
asset = bpy.data.collections.new('PC_PSX_Repairable')
bpy.context.scene.collection.children.link(asset)
root = bpy.data.objects.new('PC_PSX_Repairable', None)
asset.objects.link(root)
root['description'] = 'Front = -Y; removable cover = -X; up = +Z. Units: metres.'
root['panel_removal_axis'] = '-X in Blender local space'

def own(o):
    for c in list(o.users_collection): c.objects.unlink(o)
    asset.objects.link(o)
    o.parent = root
    return o

def subset(name, indices):
    obj = bpy.data.objects.new(name, source.data.copy())
    asset.objects.link(obj)
    obj.parent = root
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in indices], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(obj.data); bm.free(); obj.data.update()
    return obj

def material(name, color, metallic=0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metallic
    p.inputs['Roughness'].default_value=.8
    return m

metal=material('Chassis painted steel',(.33,.35,.36),.15)
dark=material('Mounting hardware',(.075,.085,.095),.2)

def box(name, loc, scale, mat=metal):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o=own(bpy.context.object); o.name=name; o.dimensions=scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(mat)
    return o

def solidify(o, thickness):
    bpy.context.view_layer.objects.active=o
    mod=o.modifiers.new('Sheet metal thickness','SOLIDIFY')
    mod.thickness=thickness; mod.offset=-1
    o.data.materials.append(metal)
    mod.material_offset=len(o.data.materials)-1
    mod.material_offset_rim=len(o.data.materials)-1
    bpy.ops.object.modifier_apply(modifier=mod.name)

# The first connected island is the bevelled main shell. Select geometrically,
# preserving original face-corner UVs by copying the mesh before deleting faces.
mesh=source.data
shell_vertices=set(); pending=[0]
adj={v.index:set() for v in mesh.vertices}
for e in mesh.edges:
    a,b=e.vertices; adj[a].add(b); adj[b].add(a)
while pending:
    v=pending.pop()
    if v in shell_vertices: continue
    shell_vertices.add(v); pending.extend(adj[v]-shell_vertices)
shell_faces={p.index for p in mesh.polygons if p.vertices[0] in shell_vertices}
cover_faces={p.index for p in mesh.polygons if p.index in shell_faces and p.normal.x < -.99}
assert len(cover_faces)==2, 'Source side panel topology changed; inspect before rebuilding.'
fan_faces={p.index for p in mesh.polygons if all(mesh.vertices[i].co.x < -.0961 for i in p.vertices)}
# Front trim is farther forward than the side grille; exclude it explicitly.
fan_faces={i for i in fan_faces if mesh.polygons[i].center.y > -.1}
assert len(fan_faces)==72
shell=subset('Case_Shell',shell_faces-cover_faces)
solidify(shell,.0015)
cover=subset('SidePanel',cover_faces)
solidify(cover,.0015)
fan=subset('SidePanel_Grille',fan_faces)
fan.parent=cover
details=subset('Case_ExteriorDetails',set(range(len(mesh.polygons)))-shell_faces-cover_faces-fan_faces)
bpy.data.objects.remove(source, do_unlink=True)

# Place the cover origin at its centre, with the grille following the same pivot.
bpy.context.view_layer.update()
centre=Vector((-.096,0,.235))
for v in cover.data.vertices: v.co-=centre
cover.location=centre
for v in fan.data.vertices: v.co-=centre
cover['removal_distance_m']=.22
cover['closed_local_position']=list(centre)
cover['role']='Detach this object and its child grille; no hidden wall remains behind it.'

# Frame stays behind the edge of the opening, so it cannot block component access.
for y in (-.193,.193):
    box('Frame_Pillar_'+('Front' if y<0 else 'Rear'),(-.085,y,.235),(.012,.012,.423))
for z in (.03,.442):
    box('Frame_Rail_'+('Lower' if z<.1 else 'Upper'),(-.085,0,z),(.012,.38,.012))
tray=box('Motherboard_Tray',(.076,.025,.184),(.002,.308,.284))
for y in (-.11,.02,.16):
    for z in (.065,.185,.305):
        box('Motherboard_Standoff',(.069,y,z),(.012,.006,.006),dark)
# Rear/top PSU shelf, leaving clearance for the motherboard beneath it.
box('PSU_Shelf',(.0,.123,.337),(.173,.15,.004))
for x in (-.077,.077):
    box('PSU_Support', (x,.12,.325),(.008,.145,.02))
# Two narrow front drive rails, not a solid block occupying the drive bay.
for x in (-.08,.08):
    box('DriveBay_Rail', (x,-.125,.353),(.008,.145,.012))

mounts=bpy.data.objects.new('ComponentMounts',None); asset.objects.link(mounts); mounts.parent=root
mount_specs={
 'Mount_Motherboard':((.061,.025,.184),(.003,.285,.265)),
 'Mount_PSU':((0,.123,.386),(.15,.14,.086)),
 'Mount_GPU':((-.005,.045,.135),(.13,.245,.035)),
 'Mount_RAM_A':((.043,-.065,.24),(.03,.006,.135)),
 'Mount_RAM_B':((.043,-.05,.24),(.03,.006,.135)),
 'Mount_Drive':((0,-.12,.38),(.145,.15,.04)),
}
for name,(loc,size) in mount_specs.items():
    o=bpy.data.objects.new(name,None); asset.objects.link(o); o.parent=mounts
    o.location=loc; o.empty_display_type='CUBE'; o.empty_display_size=.5; o.scale=size
    o['purpose']='Placement guide only; align imported component geometry to this mount.'
    o['clearance_size_metres']=list(size)

# Embed the original pixels in the blend and supply the same atlas beside FBX.
atlas=next(i for i in bpy.data.images if i.name=='PC_PSX_atlas.png')
atlas.filepath_raw=str(OUT/'PC_PSX_atlas.png'); atlas.file_format='PNG'; atlas.save()
atlas.pack()
for o in asset.objects:
    if o.type=='MESH':
        for p in o.data.polygons: p.use_smooth=False

bpy.context.view_layer.update()
assert len(cover.data.vertices)>0 and len(shell.data.vertices)>0
assert fan.parent==cover
assert all(o.scale == Vector((1,1,1)) for o in asset.objects if o.type=='MESH')

# Export only the game hierarchy, never the original studio cameras or lights.
bpy.ops.object.select_all(action='DESELECT')
for o in asset.objects: o.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.fbx(filepath=str(OUT/'PC_PSX_Repairable.fbx'),use_selection=True,
    object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,
    apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,path_mode='COPY',embed_textures=True)

scene=bpy.context.scene
scene.render.engine='CYCLES'; scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.render.resolution_x=1000; scene.render.resolution_y=1000; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.camera=bpy.data.objects.get('Camera | reference angle')
scene.camera.data.type='ORTHO'; scene.camera.data.ortho_scale=.86
target=Vector((-.07,0,.235))
scene.camera.location=(-.85,-.85,.64)
scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler()
# Save the authoring file in its assembled state; previews move only a render copy in memory.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.region_3d.view_distance=1.05
            area.spaces.active.region_3d.view_location=Vector((0,0,.23))
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'PC_PSX_Repairable.blend'))
scene.render.filepath=str(ART/'closed.png'); bpy.ops.render.render(write_still=True)
cover.location.x-=.23
scene.camera.data.ortho_scale=.94
scene.render.filepath=str(ART/'open.png'); bpy.ops.render.render(write_still=True)
report={'source_sha256':before,'source_unchanged':hashlib.sha256(SOURCE.read_bytes()).hexdigest()==before,
 'mesh_objects':sum(o.type=='MESH' for o in asset.objects),
 'triangles':sum(len(p.vertices)-2 for o in asset.objects if o.type=='MESH' for p in o.data.polygons),
 'mounts':list(mount_specs),'side_panel_faces_in_original':len(cover_faces),
 'output_blend':str(ART/'PC_PSX_Repairable.blend'),'output_fbx':str(OUT/'PC_PSX_Repairable.fbx')}
(ART/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
assert report['source_unchanged']
print('REPAIRABLE_CASE_COMPLETE',json.dumps(report))
