#!/bin/bash

# LvlUp Unity SDK - Repository Setup Script
# This script helps you move the Unity SDK to a separate repository

echo "🎮 LvlUp Unity SDK - Repository Setup"
echo "======================================"
echo ""

# Check if we're in the right directory
if [ ! -f "package.json" ]; then
    echo "❌ Error: package.json not found. Please run this from the unity-sdk folder."
    exit 1
fi

# Get GitHub username
echo "📝 Enter your GitHub username:"
read GITHUB_USERNAME

# Get repository name (default: lvlup-unity-sdk)
echo "📝 Enter repository name (press Enter for 'lvlup-unity-sdk'):"
read REPO_NAME
REPO_NAME=${REPO_NAME:-lvlup-unity-sdk}

# Confirm
echo ""
echo "Repository will be created at:"
echo "https://github.com/$GITHUB_USERNAME/$REPO_NAME"
echo ""
echo "Continue? (y/n)"
read CONFIRM

if [ "$CONFIRM" != "y" ]; then
    echo "❌ Cancelled."
    exit 0
fi

echo ""
echo "🚀 Setting up repository..."
echo ""

# Initialize git if not already initialized
if [ ! -d ".git" ]; then
    echo "📦 Initializing git repository..."
    git init
    echo "✅ Git initialized"
else
    echo "✅ Git already initialized"
fi

# Add .gitignore if it doesn't exist
if [ ! -f ".gitignore" ]; then
    echo "📝 Creating .gitignore..."
    # .gitignore should already be created by the previous step
    echo "✅ .gitignore created"
fi

# Add all files
echo "📦 Adding files to git..."
git add .

# Check if there are changes to commit
if git diff-index --quiet HEAD -- 2>/dev/null; then
    echo "ℹ️  No changes to commit"
else
    echo "📝 Committing files..."
    git commit -m "Initial commit: LvlUp Unity SDK v1.0.0

- Complete Unity SDK for LvlUp Analytics Platform
- Session management
- Event tracking
- Player journey
- Offline support
- Full documentation
"
    echo "✅ Initial commit created"
fi

# Rename branch to main
echo "🌿 Renaming branch to main..."
git branch -M main
echo "✅ Branch renamed"

# Add remote
echo "🔗 Adding remote repository..."
git remote remove origin 2>/dev/null
git remote add origin "https://github.com/$GITHUB_USERNAME/$REPO_NAME.git"
echo "✅ Remote added"

echo ""
echo "⚠️  IMPORTANT: Before proceeding, make sure you've created the repository on GitHub:"
echo "   1. Go to https://github.com/new"
echo "   2. Repository name: $REPO_NAME"
echo "   3. Make it Public or Private"
echo "   4. DON'T initialize with README, .gitignore, or license"
echo "   5. Click 'Create repository'"
echo ""
echo "Have you created the repository on GitHub? (y/n)"
read CREATED

if [ "$CREATED" != "y" ]; then
    echo ""
    echo "📋 Manual steps to complete:"
    echo "   1. Create repository on GitHub"
    echo "   2. Run: git push -u origin main"
    echo "   3. Run: git tag -a v1.0.0 -m 'Release v1.0.0'"
    echo "   4. Run: git push origin v1.0.0"
    exit 0
fi

# Push to GitHub
echo "⬆️  Pushing to GitHub..."
git push -u origin main

if [ $? -eq 0 ]; then
    echo "✅ Successfully pushed to GitHub!"
    
    # Create and push tag
    echo "🏷️  Creating release tag..."
    git tag -a v1.0.0 -m "Release v1.0.0 - Initial Unity SDK release"
    git push origin v1.0.0
    
    echo ""
    echo "🎉 Success! Your Unity SDK is now on GitHub!"
    echo ""
    echo "📦 Repository URL:"
    echo "   https://github.com/$GITHUB_USERNAME/$REPO_NAME"
    echo ""
    echo "📚 Unity Package Manager installation:"
    echo "   https://github.com/$GITHUB_USERNAME/$REPO_NAME.git"
    echo ""
    echo "📝 Next steps:"
    echo "   1. Update your main backend README to link to this repo"
    echo "   2. Create a release on GitHub (Settings > Releases > Create Release)"
    echo "   3. Share the installation URL with game developers"
    echo ""
else
    echo "❌ Failed to push to GitHub."
    echo "   Please check:"
    echo "   - Repository exists on GitHub"
    echo "   - You have push access"
    echo "   - Git credentials are configured"
    echo ""
    echo "   Try: git push -u origin main"
fi

