# Setting Up Unity SDK Repository

This guide helps you move the Unity SDK to its own repository.

## Quick Setup

### 1. Create New Repository on GitHub

```bash
# On GitHub:
# - Go to github.com
# - Click "New Repository"
# - Name: "lvlup-unity-sdk"
# - Description: "Official Unity SDK for LvlUp Analytics Platform"
# - Public or Private (your choice)
# - Don't initialize with README (we already have one)
```

### 2. Move SDK to New Repository

```bash
# Navigate to unity-sdk folder
cd /Users/emre/Desktop/MM-Projects/lvlup-backend/unity-sdk

# Initialize git
git init

# Add all files
git add .

# First commit
git commit -m "Initial commit: LvlUp Unity SDK v1.0.0"

# Add remote (replace with your GitHub URL)
git remote add origin https://github.com/YOUR_USERNAME/lvlup-unity-sdk.git

# Push to GitHub
git branch -M main
git push -u origin main
```

### 3. Create Release Tag

```bash
# Tag v1.0.0
git tag -a v1.0.0 -m "Release v1.0.0 - Initial Unity SDK release"
git push origin v1.0.0
```

### 4. Update Backend Repository

In your main `lvlup-backend` repo, update the README to link to the SDK:

```markdown
## Unity SDK

The Unity SDK is available in a separate repository:
https://github.com/YOUR_USERNAME/lvlup-unity-sdk

### Installation
\`\`\`
# Via Unity Package Manager
Add package from git URL:
https://github.com/YOUR_USERNAME/lvlup-unity-sdk.git
\`\`\`
```

## Repository Structure

After separation:

```
lvlup-unity-sdk/
├── .gitignore
├── LICENSE
├── README.md
├── QUICKSTART.md
├── INTEGRATION_GUIDE.md
├── API_REFERENCE.md
├── CHANGELOG.md
├── package.json
├── Runtime/
│   ├── LvlUp.Runtime.asmdef
│   └── Scripts/
│       ├── LvlUpManager.cs
│       ├── LvlUpConfig.cs
│       └── ...
└── Examples/
    ├── BasicLvlUpIntegration.cs
    └── PlayerJourneyExample.cs
```

## Unity Package Manager Integration

Once in separate repo, game developers can install via:

### Method 1: Git URL
```
https://github.com/YOUR_USERNAME/lvlup-unity-sdk.git
```

### Method 2: Git URL with Version
```
https://github.com/YOUR_USERNAME/lvlup-unity-sdk.git#v1.0.0
```

### Method 3: Git URL with Path (if needed)
```
https://github.com/YOUR_USERNAME/lvlup-unity-sdk.git?path=/Runtime
```

## Benefits of Separate Repository

✅ **Clean separation** - Backend and SDK are independent  
✅ **Easy installation** - UPM can install directly from GitHub  
✅ **Version control** - SDK has its own versioning  
✅ **Better discoverability** - Easier for Unity devs to find  
✅ **Asset Store ready** - Can submit to Unity Asset Store  
✅ **CI/CD** - Can set up Unity-specific testing  

## Maintaining Both Repos

### Backend Repo (`lvlup-backend`)
- Contains: Backend API, Frontend Dashboard
- Audience: Backend developers, DevOps
- Stack: Node.js, React, PostgreSQL

### SDK Repo (`lvlup-unity-sdk`)
- Contains: Unity SDK only
- Audience: Unity game developers
- Stack: C# for Unity

## Updating SDK After Backend Changes

When you update backend API:
1. Update backend repo
2. Update SDK repo if endpoints changed
3. Bump SDK version
4. Create new release tag
5. Update SDK CHANGELOG.md

## Optional: Monorepo Alternative

If you prefer keeping everything together, you can use a monorepo with subfolders:

```
lvlup/
├── packages/
│   ├── backend/
│   ├── frontend/
│   └── unity-sdk/
└── README.md
```

But separate repos is recommended for Unity SDKs.

---

**Recommendation: Use separate repository for better developer experience and Unity Package Manager compatibility.**

