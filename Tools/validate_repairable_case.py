import bpy, json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'ArtSource/PC_PSX_Repairable'
OUT=ROOT/'Assets/models/Pc case/repairable'
bpy.ops.wm.open_mainfile(filepath=str(ART/'PC_PSX_Repairable.blend'))
panel=bpy.data.objects['SidePanel']
assert abs(panel.location.x+.096)<1e-6, 'Saved panel must be closed.'
assert bpy.data.objects['SidePanel_Grille'].parent==panel
shell=bpy.data.objects['Case_Shell']
assert not any(p.center.x < -.09 and abs(p.normal.x) > .99 and p.area > .02 for p in shell.data.polygons), 'Opening blocked.'
assert len([o for o in bpy.data.objects if o.name.startswith('Mount_')])==6
assert any(i.packed_file for i in bpy.data.images if i.name=='PC_PSX_atlas.png')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(OUT/'PC_PSX_Repairable.fbx'))
meshes=[o for o in bpy.data.objects if o.type=='MESH']
assert len(meshes)==23
assert not any(o.type in {'LIGHT','CAMERA'} for o in bpy.data.objects)
assert bpy.data.objects['SidePanel_Grille'].parent==bpy.data.objects['SidePanel']
pts=[o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
dimensions=[max(p[i] for p in pts)-min(p[i] for p in pts) for i in range(3)]
for got,expected in zip(sorted(dimensions), sorted([.2,.443,.459])):
    assert abs(got-expected)<.002,(dimensions,expected)
missing=[i.filepath for i in bpy.data.images if i.source=='FILE' and not i.has_data]
assert not missing,missing
report=json.loads((ART/'validation.json').read_text())
report.update(fbx_roundtrip_passed=True,fbx_dimensions_metres=dimensions,
              closed_panel_verified=True,opening_verified=True,packed_atlas_verified=True)
(ART/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('VALIDATION_PASSED',json.dumps(report))
