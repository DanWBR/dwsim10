//    Copyright 2026 Daniel Wagner O. de Medeiros
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The plant model as one HTML page that opens in any browser (three.js, loaded from a CDN):
    /// orbit around it, point at an item or a line to read its operating data, find it in the list,
    /// and watch the flow move along the lines at a speed that follows their velocity. It needs no
    /// SolidCraft, so it can go to people who do not have it.
    /// </summary>
    public static class WebViewer
    {
        public static void Write(PlantModel model, string path)
        {
            var html = Template.Replace("__TITLE__", WebUtility.HtmlEncode(model.Name + " - 3D plant model"))
                .Replace("__DATA__", Data(model).ToString(Newtonsoft.Json.Formatting.None).Replace("</", "<\\/"));
            File.WriteAllText(path, html, new UTF8Encoding(false));
        }

        static JObject Data(PlantModel model)
        {
            var items = new JArray();
            foreach (var item in model.Equipment.Where(e => e.Visible))
            {
                var parts = new JArray();
                foreach (var part in item.Parts)
                {
                    var p = part.Moved(item.Origin);
                    EquipmentTemplates.ApplyFinish(p);
                    var o = new JObject { ["k"] = p.Kind.ToString(), ["c"] = p.Color ?? EquipmentTemplates.Generic, ["m"] = p.Metallic || !string.IsNullOrEmpty(p.Look), ["p"] = V(p.Point) };
                    switch (p.Kind)
                    {
                        case PrimitiveKind.Cylinder:
                            if (!(p.Diameter > 0 && p.Height > 0)) continue;
                            o["a"] = V(p.AxisVector ?? (p.Axis == "X" ? Vec3.PlusX : p.Axis == "Y" ? new Vec3(0, 1, 0) : Vec3.PlusZ));
                            o["d"] = p.Diameter; o["h"] = p.Height;
                            break;
                        case PrimitiveKind.Box:
                            if (!(p.SizeX > 0 && p.SizeY > 0 && p.SizeZ > 0)) continue;
                            o["s"] = new JArray(p.SizeX, p.SizeY, p.SizeZ);
                            break;
                        case PrimitiveKind.Sphere:
                            if (!(p.Diameter > 0)) continue;
                            o["d"] = p.Diameter;
                            break;
                        case PrimitiveKind.Head:
                            if (!(p.Diameter > 0 && p.Depth > 0) || !p.AxisVector.HasValue) continue;
                            o["a"] = V(p.AxisVector.Value); o["d"] = p.Diameter; o["h"] = p.Depth;
                            break;
                        case PrimitiveKind.Torus:
                            if (!(p.Major > p.Minor && p.Minor > 0)) continue;
                            o["R"] = p.Major; o["r"] = p.Minor;
                            break;
                    }
                    parts.Add(o);
                }
                items.Add(new JObject
                {
                    ["tag"] = item.Tag,
                    ["desc"] = item.Description,
                    ["inst"] = item.IsInstrument,
                    ["props"] = Props(item.Properties),
                    ["parts"] = parts
                });
            }

            var lines = new JArray();
            foreach (var run in model.Pipes.Where(r => r.Path.Count > 1))
                lines.Add(new JObject
                {
                    ["tag"] = run.Tag,
                    ["desc"] = "Line " + run.Tag + " (" + run.Size + ", " + run.Phase.ToLowerInvariant() + "), " + run.From + " to " + run.To,
                    ["c"] = run.Color,
                    ["od"] = run.OuterDiameter,
                    ["ins"] = run.Insulation,
                    ["v"] = run.Velocity,
                    ["path"] = new JArray(run.Path.Select(V)),
                    ["fl"] = new JArray(run.Flanges.Select(f => new JArray(V(f.Item1), V(f.Item2)))),
                    ["props"] = Props(run.Properties)
                });

            var data = new JObject { ["name"] = model.Name, ["items"] = items, ["lines"] = lines };
            if (model.Rack != null)
                data["rack"] = new JObject
                {
                    ["x0"] = model.Rack.X0, ["x1"] = model.Rack.X1, ["y0"] = model.Rack.Y0, ["w"] = model.Rack.Width,
                    ["top"] = model.Rack.Top, ["bay"] = PipeRack.BayLength, ["beam"] = PipeRack.BeamHeight
                };
            return data;
        }

        static JArray V(Vec3 v) => new JArray(Math.Round(v.X, 1), Math.Round(v.Y, 1), Math.Round(v.Z, 1));

        static JObject Props(System.Collections.Generic.Dictionary<string, string> props)
        {
            var o = new JObject();
            foreach (var kv in props) o[kv.Key] = kv.Value;
            return o;
        }

        const string Template = @"<!doctype html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>__TITLE__</title>
<style>
  :root { --bg: #eef1f4; --panel: #ffffff; --text: #1d2733; --muted: #5d6b7a; --line: #d5dbe1; --accent: #2f6db5; }
  @media (prefers-color-scheme: dark) { :root { --bg: #161b21; --panel: #1f262e; --text: #e3e8ee; --muted: #94a1af; --line: #333d48; --accent: #6ea4e6; } }
  html, body { margin: 0; height: 100%; background: var(--bg); color: var(--text); font: 13px/1.4 system-ui, -apple-system, 'Segoe UI', Arial, sans-serif; overflow: hidden; }
  #view { position: absolute; inset: 0; }
  #side { position: absolute; top: 12px; left: 12px; bottom: 12px; width: 260px; background: var(--panel); border: 1px solid var(--line); border-radius: 8px; display: flex; flex-direction: column; }
  #side h1 { font-size: 14px; margin: 12px 12px 4px; }
  #side .sub { color: var(--muted); margin: 0 12px 8px; }
  #side .opts { padding: 0 12px 8px; border-bottom: 1px solid var(--line); display: grid; gap: 4px; }
  #side input[type=search] { margin: 8px 12px; padding: 6px 8px; border: 1px solid var(--line); border-radius: 6px; background: var(--bg); color: var(--text); }
  #list { overflow: auto; flex: 1; padding: 0 4px 8px; }
  #list div { padding: 4px 8px; border-radius: 5px; cursor: pointer; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  #list div:hover, #list div.sel { background: var(--bg); }
  #list .grp { color: var(--muted); font-weight: 600; cursor: default; margin-top: 6px; }
  #list .grp:hover { background: none; }
  #info { position: absolute; right: 12px; top: 12px; width: 300px; max-height: calc(100% - 24px); overflow: auto; background: var(--panel); border: 1px solid var(--line); border-radius: 8px; padding: 10px 12px; display: none; }
  #info h2 { font-size: 14px; margin: 0 0 2px; }
  #info .sub { color: var(--muted); margin-bottom: 8px; }
  #info table { border-collapse: collapse; width: 100%; }
  #info td { padding: 2px 0; vertical-align: top; border-top: 1px solid var(--line); }
  #info td:first-child { color: var(--muted); padding-right: 8px; }
  #tip { position: absolute; pointer-events: none; background: var(--panel); border: 1px solid var(--line); border-radius: 5px; padding: 2px 6px; display: none; }
  #err { position: absolute; inset: 0; display: none; align-items: center; justify-content: center; text-align: center; padding: 16px; color: var(--muted); }
  @media (max-width: 700px) { #side { width: auto; right: 12px; bottom: auto; max-height: 40%; } #info { top: auto; bottom: 12px; left: 12px; width: auto; max-height: 40%; } }
</style>
<script type=""importmap"">{ ""imports"": { ""three"": ""https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js"", ""three/addons/"": ""https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/"" } }</script>
</head>
<body>
<div id=""view""></div>
<div id=""side"">
  <h1 id=""title""></h1>
  <div class=""sub"">DWSIM 3D plant model</div>
  <div class=""opts"">
    <label><input type=""checkbox"" id=""optFlow"" checked> Flow animation</label>
    <label><input type=""checkbox"" id=""optIns"" checked> Insulation</label>
    <label><input type=""checkbox"" id=""optRack"" checked> Pipe rack</label>
  </div>
  <input type=""search"" id=""find"" placeholder=""Find an item or a line"">
  <div id=""list""></div>
</div>
<div id=""info""></div>
<div id=""tip""></div>
<div id=""err"">The viewer needs a browser with WebGL and an internet connection the first time (three.js comes from cdn.jsdelivr.net).</div>
<script type=""application/json"" id=""data"">__DATA__</script>
<script type=""module"">
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';

const D = JSON.parse(document.getElementById('data').textContent);
document.getElementById('title').textContent = D.name;
const mm = 0.001;
const v3 = a => new THREE.Vector3(a[0] * mm, a[1] * mm, a[2] * mm);
const dark = matchMedia('(prefers-color-scheme: dark)').matches;

let renderer;
try { renderer = new THREE.WebGLRenderer({ antialias: true }); } catch (e) { document.getElementById('err').style.display = 'flex'; throw e; }
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.toneMapping = THREE.ACESFilmicToneMapping;
document.getElementById('view').appendChild(renderer.domElement);
const scene = new THREE.Scene();
scene.background = new THREE.Color(dark ? 0x161b21 : 0xeef1f4);
scene.environment = new THREE.PMREMGenerator(renderer).fromScene(new RoomEnvironment(), 0.04).texture;
scene.add(new THREE.HemisphereLight(0xffffff, 0x8090a0, 0.6));
const sun = new THREE.DirectionalLight(0xffffff, 1.6); sun.position.set(-30, -40, 60); scene.add(sun);

const camera = new THREE.PerspectiveCamera(40, 1, 0.05, 5000);
camera.up.set(0, 0, 1);
const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;

const mats = new Map();
function mat(color, metal) {
  const key = color + metal;
  if (!mats.has(key)) mats.set(key, new THREE.MeshStandardMaterial({ color, metalness: metal ? 0.6 : 0.05, roughness: metal ? 0.4 : 0.75 }));
  return mats.get(key);
}
const up = new THREE.Vector3(0, 1, 0);
function along(mesh, axis) { mesh.quaternion.setFromUnitVectors(up, axis.clone().normalize()); }
function cylinder(a, b, r, m, seg = 24) {
  const d = b.clone().sub(a), l = d.length();
  if (l < 1e-6) return null;
  const mesh = new THREE.Mesh(new THREE.CylinderGeometry(r, r, l, seg), m);
  along(mesh, d); mesh.position.copy(a).addScaledVector(d, 0.5);
  return mesh;
}

const pickables = [], entries = [];
const groups = { rack: new THREE.Group(), ins: new THREE.Group() };
scene.add(groups.rack, groups.ins);

for (const it of D.items) {
  const g = new THREE.Group();
  for (const p of it.parts) {
    const m = mat(p.c, p.m), at = v3(p.p);
    let mesh = null;
    if (p.k === 'Cylinder') mesh = cylinder(at, at.clone().addScaledVector(new THREE.Vector3(...p.a).normalize(), p.h * mm), p.d * mm / 2, m, 32);
    else if (p.k === 'Box') { mesh = new THREE.Mesh(new THREE.BoxGeometry(p.s[0] * mm, p.s[1] * mm, p.s[2] * mm), m); mesh.position.copy(at); }
    else if (p.k === 'Sphere') { mesh = new THREE.Mesh(new THREE.SphereGeometry(p.d * mm / 2, 24, 16), m); mesh.position.copy(at); }
    else if (p.k === 'Torus') { mesh = new THREE.Mesh(new THREE.TorusGeometry(p.R * mm, p.r * mm, 8, 48), m); mesh.position.copy(at); }
    else if (p.k === 'Head') {
      const r = p.d * mm / 2;
      mesh = new THREE.Mesh(new THREE.SphereGeometry(r, 32, 12, 0, Math.PI * 2, 0, Math.PI / 2), m);
      mesh.scale.set(1, p.h * mm / r, 1); along(mesh, new THREE.Vector3(...p.a)); mesh.position.copy(at);
    }
    if (mesh) g.add(mesh);
  }
  scene.add(g);
  const e = { kind: it.inst ? 'Instruments' : 'Equipment', tag: it.tag, desc: it.desc, props: it.props, obj: g };
  g.traverse(o => { if (o.isMesh) { o.userData.entry = e; pickables.push(o); } });
  entries.push(e);
}

const flows = [];
const cladding = mat('#c4cad1', true), flangeMat = mat('#6b7075', true);
for (const ln of D.lines) {
  const g = new THREE.Group(), m = mat(ln.c, true), r = ln.od * mm / 2;
  const pts = ln.path.map(v3);
  for (let i = 0; i + 1 < pts.length; i++) { const c = cylinder(pts[i], pts[i + 1], r, m, 16); if (c) g.add(c); }
  for (let i = 1; i + 1 < pts.length; i++) { const s = new THREE.Mesh(new THREE.SphereGeometry(r, 16, 12), m); s.position.copy(pts[i]); g.add(s); }
  for (const f of ln.fl) {
    const face = v3(f[0]), dir = new THREE.Vector3(...f[1]).normalize(), t = Math.max(30, 0.25 * ln.od) * mm;
    const c = cylinder(face.clone().addScaledVector(dir, -t / 2), face.clone().addScaledVector(dir, t / 2), r * 1.7, flangeMat, 24);
    if (c) g.add(c);
  }
  if (ln.ins > 0) {
    const ri = r + ln.ins * mm;
    for (let i = 0; i + 1 < pts.length; i++) { const c = cylinder(pts[i], pts[i + 1], ri, cladding, 16); if (c) groups.ins.add(c); }
    for (let i = 1; i + 1 < pts.length; i++) { const s = new THREE.Mesh(new THREE.SphereGeometry(ri, 16, 12), cladding); s.position.copy(pts[i]); groups.ins.add(s); }
  }
  scene.add(g);
  const e = { kind: 'Lines', tag: ln.tag, desc: ln.desc, props: ln.props, obj: g };
  g.traverse(o => { if (o.isMesh) { o.userData.entry = e; pickables.push(o); } });
  entries.push(e);

  // flow: short bright dashes along the route, at a pace that follows the velocity
  const curve = new THREE.CurvePath();
  for (let i = 0; i + 1 < pts.length; i++) if (pts[i].distanceTo(pts[i + 1]) > 1e-6) curve.add(new THREE.LineCurve3(pts[i], pts[i + 1]));
  if (curve.curves.length === 0) continue;
  const length = curve.getLength(), n = Math.max(2, Math.round(length / 1.2));
  const dash = new THREE.InstancedMesh(new THREE.SphereGeometry(Math.max(r * 1.15, 0.03), 10, 8), new THREE.MeshBasicMaterial({ color: new THREE.Color(ln.c).offsetHSL(0, 0, 0.3) }), n);
  scene.add(dash);
  flows.push({ curve, length, n, dash, speed: 0.3 + Math.min(4, Math.max(0, ln.v || 0)) * 0.6 });
}

if (D.rack) {
  const k = D.rack, steel = mat('#6b7075', true), zb = (k.top - k.beam / 2) * mm, ya = k.y0 * mm, yb = (k.y0 + k.w) * mm;
  const bays = Math.max(1, Math.ceil((k.x1 - k.x0) / k.bay)), bay = (k.x1 - k.x0) * mm / bays, w = 0.15;
  const beam = (a, b) => { const c = cylinder(a, b, w / 2, steel, 6); if (c) groups.rack.add(c); };
  for (let i = 0; i <= bays; i++) {
    const x = k.x0 * mm + i * bay;
    beam(new THREE.Vector3(x, ya, 0), new THREE.Vector3(x, ya, zb));
    beam(new THREE.Vector3(x, yb, 0), new THREE.Vector3(x, yb, zb));
    beam(new THREE.Vector3(x, ya, zb), new THREE.Vector3(x, yb, zb));
  }
  beam(new THREE.Vector3(k.x0 * mm, ya, zb), new THREE.Vector3(k.x1 * mm, ya, zb));
  beam(new THREE.Vector3(k.x0 * mm, yb, zb), new THREE.Vector3(k.x1 * mm, yb, zb));
}

// ground and the first view: iso, from the front left
const box = new THREE.Box3();
for (const e of entries) box.expandByObject(e.obj);
box.expandByObject(groups.rack);
const centre = box.getCenter(new THREE.Vector3()), size = box.getSize(new THREE.Vector3());
const ground = new THREE.Mesh(new THREE.PlaneGeometry(size.x + 20, size.y + 20), new THREE.MeshStandardMaterial({ color: dark ? 0x2a323b : 0xd9dee3, roughness: 1 }));
ground.position.set(centre.x, centre.y, box.min.z - 0.01); scene.add(ground);
const grid = new THREE.GridHelper(Math.ceil(Math.max(size.x, size.y) + 20), Math.ceil(Math.max(size.x, size.y) + 20), dark ? 0x3a444f : 0xc2c9d0, dark ? 0x2f3842 : 0xcdd3d9);
grid.rotation.x = Math.PI / 2; grid.position.set(centre.x, centre.y, box.min.z); scene.add(grid);
const iso = new THREE.Vector3(-0.6, -0.65, 0.5).normalize();
function look(c, extent) {
  controls.target.copy(c);
  camera.position.copy(c).addScaledVector(iso, extent / 2 / Math.tan(THREE.MathUtils.degToRad(camera.fov) / 2) * 1.1 / Math.min(1, camera.aspect));
}
function home() { look(centre, size.length()); }

// list, search and the data panel
const list = document.getElementById('list'), info = document.getElementById('info'), tip = document.getElementById('tip');
let selected = null;
const esc = s => String(s).replace(/[&<>""]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '""': '&quot;' }[c]));
function fill(filter) {
  list.innerHTML = '';
  for (const kind of ['Equipment', 'Lines', 'Instruments']) {
    const rows = entries.filter(e => e.kind === kind && (!filter || (e.tag + ' ' + e.desc).toLowerCase().includes(filter)));
    if (!rows.length) continue;
    const h = document.createElement('div'); h.className = 'grp'; h.textContent = kind; list.appendChild(h);
    for (const e of rows) {
      const d = document.createElement('div'); d.textContent = e.tag; d.title = e.desc || e.tag;
      d.onclick = () => select(e, true); e.row = d; list.appendChild(d);
    }
  }
}
fill('');
document.getElementById('find').oninput = ev => fill(ev.target.value.trim().toLowerCase());

const glow = new THREE.Color(0x3d7fd6);
function tint(e, on) {
  e.obj.traverse(o => {
    if (!o.isMesh) return;
    if (on) { o.userData.mat = o.material; o.material = o.material.clone(); o.material.emissive = glow; o.material.emissiveIntensity = 0.35; }
    else if (o.userData.mat) { o.material.dispose(); o.material = o.userData.mat; }
  });
}
function select(e, focus) {
  if (selected) { tint(selected, false); selected.row && selected.row.classList.remove('sel'); }
  selected = e;
  if (!e) { info.style.display = 'none'; return; }
  tint(e, true); e.row && e.row.classList.add('sel');
  info.innerHTML = '<h2>' + esc(e.tag) + '</h2><div class=""sub"">' + esc(e.desc || '') + '</div><table>' +
    Object.entries(e.props || {}).map(([k, v]) => '<tr><td>' + esc(k) + '</td><td>' + esc(v) + '</td></tr>').join('') + '</table>';
  info.style.display = 'block';
  if (focus) {
    const b = new THREE.Box3().setFromObject(e.obj);
    look(b.getCenter(new THREE.Vector3()), Math.max(3, b.getSize(new THREE.Vector3()).length() * 1.3));
  }
}

const ray = new THREE.Raycaster(), mouse = new THREE.Vector2();
function pick(ev) {
  const r = renderer.domElement.getBoundingClientRect();
  mouse.set((ev.clientX - r.left) / r.width * 2 - 1, -(ev.clientY - r.top) / r.height * 2 + 1);
  ray.setFromCamera(mouse, camera);
  const hit = ray.intersectObjects(pickables, false)[0];
  return hit ? hit.object.userData.entry : null;
}
let down = null;
renderer.domElement.addEventListener('pointerdown', ev => { down = [ev.clientX, ev.clientY]; });
renderer.domElement.addEventListener('pointerup', ev => {
  if (down && Math.hypot(ev.clientX - down[0], ev.clientY - down[1]) < 4) select(pick(ev), false);
  down = null;
});
renderer.domElement.addEventListener('dblclick', ev => { const e = pick(ev); if (e) select(e, true); else home(); });
renderer.domElement.addEventListener('pointermove', ev => {
  const e = pick(ev);
  if (!e) { tip.style.display = 'none'; return; }
  tip.textContent = e.tag; tip.style.left = (ev.clientX + 12) + 'px'; tip.style.top = (ev.clientY + 12) + 'px'; tip.style.display = 'block';
});

const optFlow = document.getElementById('optFlow');
document.getElementById('optIns').onchange = ev => groups.ins.visible = ev.target.checked;
document.getElementById('optRack').onchange = ev => groups.rack.visible = ev.target.checked;

function resize() {
  const w = innerWidth, h = innerHeight;
  renderer.setSize(w, h); camera.aspect = w / h; camera.updateProjectionMatrix();
}
addEventListener('resize', resize); resize(); home();

const m4 = new THREE.Matrix4(), clock = new THREE.Clock();
renderer.setAnimationLoop(() => {
  const t = clock.getElapsedTime();
  for (const f of flows) {
    f.dash.visible = optFlow.checked;
    if (!optFlow.checked) continue;
    for (let i = 0; i < f.n; i++) {
      const u = ((i / f.n) + t * f.speed / f.length) % 1;
      m4.makeTranslation(f.curve.getPointAt(u));
      f.dash.setMatrixAt(i, m4);
    }
    f.dash.instanceMatrix.needsUpdate = true;
  }
  controls.update();
  renderer.render(scene, camera);
});
</script>
</body>
</html>
";
    }
}
