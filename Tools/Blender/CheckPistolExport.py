import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import bpy

project = Path(bpy.data.filepath).resolve().parents[2]
assert (project / 'ProjectSettings/ProjectVersion.txt').is_file()
report_folder = project / '.codex/animation-checks'
report_folder.mkdir(parents=True, exist_ok=True)
for library in bpy.data.libraries:
    assert library.filepath.startswith('//') and Path(bpy.path.abspath(library.filepath)).is_file()
for image in bpy.data.images:
    if image.source == 'FILE':
        assert image.packed_file and len(image.pixels) > 0, image.name

tool = {}
exec(bpy.data.texts['Pistol Animation Tools.py'].as_string(), tool)
reports = []
with tempfile.TemporaryDirectory(prefix='pistol-export-', dir=report_folder) as temporary:
    fixture = Path(temporary).resolve()
    assert fixture.is_relative_to(report_folder.resolve())
    (fixture / 'ProjectSettings').mkdir()
    shutil.copy2(project / 'ProjectSettings/ProjectVersion.txt', fixture / 'ProjectSettings/ProjectVersion.txt')
    for action in tool['ACTIONS']:
        _, originals = tool['unity_destinations'](action, str(project))
        for original in originals.values():
            target = fixture / original.relative_to(project)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(original, target)
            shutil.copy2(str(original) + '.meta', str(target) + '.meta')
        state = tool['capture_state']()
        paths = tool['export_to_unity'](action, str(fixture))
        restored = tool['capture_state']()
        assert state['scene'] == restored['scene'] and state['mode'] == restored['mode']
        assert state['scenes'] == restored['scenes']
        assert state['rigs'] == restored['rigs']
        assert state['weapon_basis'] == restored['weapon_basis']
        assert state['attachment_mute'] == restored['attachment_mute']
        for path in paths:
            original = project / path.relative_to(fixture)
            assert Path(str(path)+'.meta').read_bytes() == Path(str(original)+'.meta').read_bytes()
        reports.append({'action': action, 'authoringStateRestored': True, 'metadataPreserved': True})

    _, targets = tool['unity_destinations']('RELOAD_EMPTY', str(fixture))
    hashes = {path: hashlib.sha256(path.read_bytes()).hexdigest() for path in targets.values()}
    replace = tool['replace_fbx']
    calls = 0
    def fail_second_copy(source, target):
        global calls
        calls += 1
        if calls == 2:
            raise OSError('Intentional second-file failure')
        replace(source, target)
    tool['replace_fbx'] = fail_second_copy
    try:
        tool['export_to_unity']('RELOAD_EMPTY', str(fixture))
        raise AssertionError('Copy failure was swallowed')
    except OSError:
        assert all(hashlib.sha256(path.read_bytes()).hexdigest() == digest for path, digest in hashes.items())
    finally:
        tool['replace_fbx'] = replace
    next(iter(targets.values())).with_suffix('.fbx.meta').unlink()
    try:
        tool['unity_destinations']('RELOAD_EMPTY', str(fixture))
        raise AssertionError('Missing metadata was accepted')
    except RuntimeError:
        pass

try:
    tool['unity_destinations']('RELOAD_EMPTY', '//not-a-project')
    raise AssertionError('Invalid project was accepted')
except RuntimeError:
    pass
result = {'presets': reports, 'partialCopyRollback': True, 'missingMetadataRejected': True,
          'invalidProjectRejected': True, 'relativeLibraryAndPackedImagesVerified': True}
(report_folder / 'exporter-regression.json').write_text(json.dumps(result, indent=2))
print('PISTOL_EXPORT_CHECKS_PASSED', json.dumps(result))
