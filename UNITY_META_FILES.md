# Unity .meta Files

Unity automatically generates `.meta` files for all assets. When you import the LvlUp SDK into Unity, these will be created automatically.

## What are .meta files?

Unity uses `.meta` files to:
- Track asset GUIDs (unique identifiers)
- Store import settings
- Maintain references between assets
- Handle version control

## Important Notes

1. **Version Control**: Always commit `.meta` files with your code
2. **Don't Edit**: These files are managed by Unity
3. **Auto-Generated**: Unity creates them when you import assets
4. **Required**: Missing `.meta` files can break asset references

## When Importing SDK

After copying the SDK to your Unity project:

1. Unity will automatically generate `.meta` files
2. You'll see them next to each file/folder
3. They're text files containing GUIDs and settings

Example structure after import:
```
Assets/
  └── LvlUp/
      ├── LvlUp.meta
      ├── Runtime/
      │   ├── Runtime.meta
      │   ├── Scripts/
      │   │   ├── Scripts.meta
      │   │   ├── LvlUpManager.cs
      │   │   ├── LvlUpManager.cs.meta
      │   │   └── ...
      └── Examples/
          └── ...
```

## For Git Users

Add this to your `.gitignore` if Unity generates other unnecessary files:
```
# But DO commit .meta files!
# DON'T add *.meta to .gitignore

# Ignore these instead:
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
```

## Troubleshooting

**Problem**: "Missing script references" or "Can't find asset"
**Solution**: 
1. Delete the Library folder
2. Reopen Unity project
3. Let Unity regenerate .meta files

**Problem**: Conflicts in version control
**Solution**:
1. Always pull .meta files with code
2. If conflict occurs, reimport the asset in Unity
3. Unity will regenerate the correct .meta file

