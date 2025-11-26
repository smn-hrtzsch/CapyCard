#!/bin/bash

# Ensure the script is run from the project root
if [ ! -d "CapyCard" ]; then
    echo "Error: Please run this script from the project root directory (where CapyCard/ is located)."
    exit 1
fi

OUTPUT_CONTENT=""

# CapyCard/CapyCard/Views/LearnView.axaml
OUTPUT_CONTENT+="--- CapyCard/CapyCard/Views/LearnView.axaml ---
$(cat CapyCard/CapyCard/Views/LearnView.axaml)

"

# CapyCard/CapyCard/Views/LearnView.axaml.cs
OUTPUT_CONTENT+="--- CapyCard/CapyCard/Views/LearnView.axaml.cs ---
$(cat CapyCard/CapyCard/Views/LearnView.axaml.cs)

"

# CapyCard/CapyCard/ViewModels/LearnViewModel.cs
OUTPUT_CONTENT+="--- CapyCard/CapyCard/ViewModels/LearnViewModel.cs ---
$(cat CapyCard/CapyCard/ViewModels/LearnViewModel.cs)

"

# CapyCard/CapyCard/Models/Card.cs
OUTPUT_CONTENT+="--- CapyCard/CapyCard/Models/Card.cs ---
$(cat CapyCard/CapyCard/Models/Card.cs)

"

# CapyCard/CapyCard/Models/Deck.cs
OUTPUT_CONTENT+="--- CapyCard/CapyCard/Models/Deck.cs ---
$(cat CapyCard/CapyCard/Models/Deck.cs)

"

# CapyCard/CapyCard/Models/LearningSession.cs
OUTPUT_CONTENT+="--- CapyCard/CapyCard/Models/LearningSession.cs ---
$(cat CapyCard/CapyCard/Models/LearningSession.cs)

"

# CapyCard/CapyCard/Data/FlashcardDbContext.cs
OUTPUT_CONTENT+="--- CapyCard/CapyCard/Data/FlashcardDbContext.cs ---
$(cat CapyCard/CapyCard/Data/FlashcardDbContext.cs)

"

echo "$OUTPUT_CONTENT"

# Copy to clipboard if pbcopy is available (macOS)
if command -v pbcopy &> /dev/null; then
    echo "$OUTPUT_CONTENT" | pbcopy
    echo "The content has been copied to your clipboard."
else
    echo "pbcopy not found. Please copy the above content manually."
fi
