#!/bin/bash

# Ensure the script is run from the project root
if [ ! -d "FlashcardMobile" ]; then
    echo "Error: Please run this script from the project root directory (where FlashcardMobile/ is located)."
    exit 1
fi

OUTPUT_CONTENT=""

# FlashcardMobile/FlashcardMobile/Views/LearnView.axaml
OUTPUT_CONTENT+="--- FlashcardMobile/FlashcardMobile/Views/LearnView.axaml ---
$(cat FlashcardMobile/FlashcardMobile/Views/LearnView.axaml)

"

# FlashcardMobile/FlashcardMobile/Views/LearnView.axaml.cs
OUTPUT_CONTENT+="--- FlashcardMobile/FlashcardMobile/Views/LearnView.axaml.cs ---
$(cat FlashcardMobile/FlashcardMobile/Views/LearnView.axaml.cs)

"

# FlashcardMobile/FlashcardMobile/ViewModels/LearnViewModel.cs
OUTPUT_CONTENT+="--- FlashcardMobile/FlashcardMobile/ViewModels/LearnViewModel.cs ---
$(cat FlashcardMobile/FlashcardMobile/ViewModels/LearnViewModel.cs)

"

# FlashcardMobile/FlashcardMobile/Models/Card.cs
OUTPUT_CONTENT+="--- FlashcardMobile/FlashcardMobile/Models/Card.cs ---
$(cat FlashcardMobile/FlashcardMobile/Models/Card.cs)

"

# FlashcardMobile/FlashcardMobile/Models/Deck.cs
OUTPUT_CONTENT+="--- FlashcardMobile/FlashcardMobile/Models/Deck.cs ---
$(cat FlashcardMobile/FlashcardMobile/Models/Deck.cs)

"

# FlashcardMobile/FlashcardMobile/Models/LearningSession.cs
OUTPUT_CONTENT+="--- FlashcardMobile/FlashcardMobile/Models/LearningSession.cs ---
$(cat FlashcardMobile/FlashcardMobile/Models/LearningSession.cs)

"

# FlashcardMobile/FlashcardMobile/Data/FlashcardDbContext.cs
OUTPUT_CONTENT+="--- FlashcardMobile/FlashcardMobile/Data/FlashcardDbContext.cs ---
$(cat FlashcardMobile/FlashcardMobile/Data/FlashcardDbContext.cs)

"

echo "$OUTPUT_CONTENT"

# Copy to clipboard if pbcopy is available (macOS)
if command -v pbcopy &> /dev/null; then
    echo "$OUTPUT_CONTENT" | pbcopy
    echo "The content has been copied to your clipboard."
else
    echo "pbcopy not found. Please copy the above content manually."
fi
