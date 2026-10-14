exec(open(r'D:\GameDesign\Hotzenplotz\Unity\HP_3\Backups\StoveJoints_20261013\inside_joints.py').read().split('for axis,centers')[0])
print('ELIGIBLE',sum(inside(f) for f in bm.faces),'MATS',[(i,m.name) for i,m in enumerate(o.data.materials)])
print([(list(f.calc_center_median()),list(f.normal),f.material_index) for f in bm.faces if abs(f.calc_center_median().z-.03968976)<.0001][:10])
