// Recipe AI Helper - Modern Alpine.js Version

// Meal type mapping (matching C# MealType enum)
const MEAL_TYPE_NAMES = {
    0: 'Śniadanie',
    1: 'Obiad',
    2: 'Kolacja',
    3: 'Deser',
    4: 'Napój',
    'Sniadanie': 'Śniadanie',
    'Obiad': 'Obiad',
    'Kolacja': 'Kolacja',
    'Deser': 'Deser',
    'Napoj': 'Napój'
};

function appData() {
    return {
        // Current state
        currentTab: 'processing',

        // AI Providers
        providers: [],
        activeProvider: null,
        showProviderModal: false,
        editingProvider: null,
        providerForm: {
            name: '',
            model: '',
            isActive: false,
            priority: 10,
            maxPagesPerChunk: 3,
            supportsDirectPDF: false
        },

        // PDF Processing
        pdfDirectory: '',
        pdfFiles: [],
        selectedPdfFiles: [],
        dirMessage: '',
        dirMessageSuccess: false,
        processingStatus: null,

        // Manual File Upload
        uploadedFiles: [],
        showProcessedFileModal: false,
        processedFileToConfirm: null,
        pendingFilesToProcess: [],

        // Recipes
        recipes: [],
        filteredRecipes: [],
        searchQuery: '',
        selectedRecipe: null,
        showRecipeModal: false,
        currentRecipeIdForImage: null,

        // Advanced recipe filters
        recipeFilters: {
            mealTypes: [],
            calories: { min: 0, max: 3000 },
            protein: { min: 0, max: 200 },
            carbs: { min: 0, max: 300 },
            fat: { min: 0, max: 150 }
        },

        // Recipe sorting
        recipeSorting: {
            field: 'name',      // name, calories, protein, carbohydrates, fat
            direction: 'asc'    // asc or desc
        },

        // Image generation
        selectedRecipeIds: [],
        isGeneratingImages: false,
        imageGenerationProgress: '',
        recipesWithoutImages: 0,

        // Image generation settings
        imageSettings: {
            provider: 'OpenAI',
            openAI: { apiKey: '***', model: 'gpt-image-1' },
            gemini: { apiKey: '***', model: 'imagen-4.0-ultra-generate-001' },
            availableProviders: ['OpenAI', 'Gemini']
        },
        imageSettingsSaving: false,
        imageSettingsTestRunning: false,

        // Todoist integration
        todoistSettings: {
            isConfigured: false,
            apiKey: '***'
        },
        todoistSettingsSaving: false,
        todoistTestRunning: false,

        // AI Model Settings for Meal Planning
        aiModelSettings: {
            recipeScaling: { model: 'gemini-2.5-flash' },
            dessertPlanning: { model: 'gemini-2.5-flash' }
        },
        isExportingToTodoist: false,

        // Meal Planner
        mealPlans: [],
        selectedPlan: null,
        editingPlanName: false,
        planNameEdit: '',
        showCreatePlanModal: false,
        showShoppingListModal: false,
        showAutoGenerateModal: false,
        showEditRecipeModal: false,
        showCreateRecipeModal: false,
        editingRecipe: null,
        newRecipe: null,
        createPlanForm: {
            name: '',
            startDate: '',
            endDate: '',
            numberOfDays: 7
        },
        autoGenerateForm: {
            categories: ['Sniadanie', 'Obiad', 'Kolacja'],
            perDay: 1,
            useCalorieTarget: false,
            targetCalories: 1800,
            calorieMargin: 200
        },
        shoppingList: null,
        isGeneratingShoppingList: false,
        isGeneratingPlan: false,
        draggedRecipe: null,
        draggedEntry: null,
        draggedFromDayId: null,
        recipeSearchQuery: '',
        filterMealType: null,
        filteredMealPlanRecipes: [],

        // Multi-person
        showPersonsModal: false,
        newPerson: {
            name: '',
            targetCalories: 2000
        },

        // Notifications
        notifications: [],

        // Initialization
        async init() {
            await this.loadProviders();
            await this.loadActiveProvider();
            await this.loadRecipes();
            await this.loadCurrentDirectory();
            await this.loadImageSettings();
            await this.loadTodoistSettings();
            await this.loadAIModelSettings();
            await this.loadMealPlans();

            // Restore last tab from localStorage
            const lastTab = localStorage.getItem('selectedTab');
            if (lastTab) {
                this.currentTab = lastTab;
            }
        },

        // ============== AI PROVIDERS ==============

        async loadProviders() {
            try {
                const response = await fetch('/api/aiproviders');
                if (!response.ok) throw new Error('Failed to load providers');
                this.providers = await response.json();
            } catch (error) {
                console.error('Error loading providers:', error);
                this.showNotification('Błąd ładowania providerów: ' + error.message, 'error');
            }
        },

        async loadActiveProvider() {
            try {
                const response = await fetch('/api/aiproviders/active');
                if (response.ok) {
                    this.activeProvider = await response.json();
                }
            } catch (error) {
                console.error('Error loading active provider:', error);
            }
        },

        resetProviderForm() {
            this.providerForm = {
                name: '',
                model: '',
                isActive: false,
                priority: 10,
                maxPagesPerChunk: 3,
                supportsDirectPDF: false
            };
        },

        updateModelSuggestion() {
            // Auto-suggest model and settings based on provider name
            if (this.providerForm.name === 'OpenAI') {
                if (!this.providerForm.model || this.editingProvider) {
                    this.providerForm.model = 'gpt-4o';
                }
                this.providerForm.maxPagesPerChunk = 3;
                this.providerForm.supportsDirectPDF = true;
            } else if (this.providerForm.name === 'Gemini' || this.providerForm.name === 'Google') {
                if (!this.providerForm.model || this.editingProvider) {
                    this.providerForm.model = 'gemini-2.5-flash';
                }
                // UWAGA: Gemini ma duży context window (1M tokenów)
                // Może przetwarzać więcej stron na raz
                this.providerForm.maxPagesPerChunk = 100;
                // UWAGA: Direct PDF w trakcie implementacji
                // Aktualnie Gemini używa konwersji PDF → obrazy
                this.providerForm.supportsDirectPDF = false;
            }
        },

        getModelHint() {
            if (this.providerForm.name === 'OpenAI') {
                return 'Sugerowane: gpt-4o, gpt-4-turbo, gpt-4';
            } else if (this.providerForm.name === 'Gemini' || this.providerForm.name === 'Google') {
                return 'Sugerowane: gemini-2.5-flash, gemini-1.5-pro';
            }
            return 'Wprowadź nazwę modelu';
        },

        editProvider(provider) {
            this.editingProvider = provider;
            this.providerForm = {
                name: provider.name,
                model: provider.model,
                isActive: provider.isActive,
                priority: provider.priority,
                maxPagesPerChunk: provider.maxPagesPerChunk,
                supportsDirectPDF: provider.supportsDirectPDF
            };
            this.showProviderModal = true;
        },

        async saveProvider() {
            try {
                let response;

                if (this.editingProvider) {
                    // Update existing provider
                    response = await fetch(`/api/aiproviders/${this.editingProvider.id}`, {
                        method: 'PUT',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            name: this.providerForm.name,
                            model: this.providerForm.model,
                            priority: parseInt(this.providerForm.priority),
                            maxPagesPerChunk: parseInt(this.providerForm.maxPagesPerChunk),
                            supportsDirectPDF: this.providerForm.supportsDirectPDF,
                            isActive: this.providerForm.isActive
                        })
                    });
                } else {
                    // Create new provider
                    response = await fetch('/api/aiproviders', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            name: this.providerForm.name,
                            model: this.providerForm.model,
                            isActive: this.providerForm.isActive,
                            priority: parseInt(this.providerForm.priority),
                            maxPagesPerChunk: parseInt(this.providerForm.maxPagesPerChunk),
                            supportsDirectPDF: this.providerForm.supportsDirectPDF
                        })
                    });
                }

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to save provider');
                }

                this.showNotification(
                    this.editingProvider ? 'Provider zaktualizowany!' : 'Provider dodany!',
                    'success'
                );

                this.showProviderModal = false;
                await this.loadProviders();
                await this.loadActiveProvider();
            } catch (error) {
                console.error('Error saving provider:', error);
                this.showNotification('Błąd zapisu: ' + error.message, 'error');
            }
        },

        async activateProvider(id) {
            try {
                const response = await fetch(`/api/aiproviders/${id}/activate`, {
                    method: 'PUT'
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to activate provider');
                }

                this.showNotification('Provider aktywowany!', 'success');
                await this.loadProviders();
                await this.loadActiveProvider();
            } catch (error) {
                console.error('Error activating provider:', error);
                this.showNotification('Błąd aktywacji: ' + error.message, 'error');
            }
        },

        async deleteProvider(id) {
            if (!confirm('Czy na pewno chcesz usunąć tego providera?')) {
                return;
            }

            try {
                const response = await fetch(`/api/aiproviders/${id}`, {
                    method: 'DELETE'
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to delete provider');
                }

                this.showNotification('Provider usunięty!', 'success');
                await this.loadProviders();
            } catch (error) {
                console.error('Error deleting provider:', error);
                this.showNotification('Błąd usuwania: ' + error.message, 'error');
            }
        },

        // ============== PDF PROCESSING ==============

        async loadCurrentDirectory() {
            try {
                const response = await fetch('/api/files/directory');
                if (!response.ok) throw new Error('Failed to load directory');

                const data = await response.json();
                this.pdfDirectory = data.directory;
            } catch (error) {
                console.error('Error loading directory:', error);
            }
        },

        async changePdfDirectory() {
            if (!this.pdfDirectory.trim()) {
                this.dirMessage = 'Ścieżka nie może być pusta!';
                this.dirMessageSuccess = false;
                return;
            }

            try {
                const response = await fetch('/api/files/directory', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ directory: this.pdfDirectory })
                });

                const data = await response.json();

                if (!response.ok) {
                    throw new Error(data.error || 'Failed to change directory');
                }

                this.dirMessage = data.message;
                this.dirMessageSuccess = true;
                this.pdfFiles = [];
                this.selectedPdfFiles = [];

                setTimeout(() => {
                    this.dirMessage = '';
                }, 5000);
            } catch (error) {
                this.dirMessage = error.message;
                this.dirMessageSuccess = false;
                console.error('Error changing directory:', error);
            }
        },

        async loadPdfFiles() {
            try {
                const response = await fetch('/api/files/list');
                if (!response.ok) {
                    const errorData = await response.json();
                    throw new Error(errorData.error || 'Failed to load files');
                }

                const data = await response.json();
                const fileNames = data.files || [];

                // Check which files have been processed
                const checkResponse = await fetch('/api/fileupload/check-files', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        directory: data.directory,
                        files: fileNames
                    })
                });

                if (checkResponse.ok) {
                    const checkData = await checkResponse.json();
                    this.pdfFiles = fileNames.map(name => ({
                        name: name,
                        processed: checkData.processedFiles[name]
                    }));
                } else {
                    // Fallback if check fails
                    this.pdfFiles = fileNames.map(name => ({ name: name, processed: null }));
                }

                this.showNotification(`Znaleziono ${this.pdfFiles.length} plików`, 'success');
            } catch (error) {
                console.error('Error loading files:', error);
                this.showNotification('Błąd ładowania plików: ' + error.message, 'error');
            }
        },

        selectAllFiles() {
            this.selectedPdfFiles = this.pdfFiles.map(f => f.name);
        },

        deselectAllFiles() {
            this.selectedPdfFiles = [];
        },

        async processSelectedFiles() {
            if (this.selectedPdfFiles.length === 0) {
                this.showNotification('Nie zaznaczono żadnych plików!', 'error');
                return;
            }

            if (!confirm(`Czy na pewno chcesz przetworzyć ${this.selectedPdfFiles.length} plików?`)) {
                return;
            }

            try {
                const response = await fetch('/api/processing/start', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ files: this.selectedPdfFiles })
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to start processing');
                }

                this.showNotification(
                    `Rozpoczęto przetwarzanie ${this.selectedPdfFiles.length} plików!`,
                    'success'
                );

                // Start monitoring processing status
                this.startStatusMonitoring();
            } catch (error) {
                console.error('Error starting processing:', error);
                this.showNotification('Błąd przetwarzania: ' + error.message, 'error');
            }
        },

        statusInterval: null,

        startStatusMonitoring() {
            if (this.statusInterval) clearInterval(this.statusInterval);

            this.statusInterval = setInterval(async () => {
                try {
                    const response = await fetch('/api/processing/status');
                    const status = await response.json();

                    // Update processing status for UI
                    this.processingStatus = status;

                    if (!status.isRunning && this.statusInterval) {
                        clearInterval(this.statusInterval);
                        this.statusInterval = null;
                        this.processingStatus = null;

                        if (status.errors === 0) {
                            this.showNotification(
                                `Przetwarzanie zakończone! Zapisano: ${status.recipesSaved} przepisów`,
                                'success'
                            );
                        } else {
                            this.showNotification(
                                `Przetwarzanie zakończone z błędami. Zapisano: ${status.recipesSaved}`,
                                'error'
                            );
                        }

                        // Refresh recipes
                        await this.loadRecipes();
                    }
                } catch (error) {
                    console.error('Error checking status:', error);
                }
            }, 500); // Check every 500ms for real-time updates
        },

        // ============== MANUAL FILE UPLOAD ==============

        async handleManualFileUpload(event) {
            const files = Array.from(event.target.files);
            if (files.length === 0) return;

            this.showNotification('Sprawdzanie plików...', 'info');

            for (const file of files) {
                const formData = new FormData();
                formData.append('file', file);

                try {
                    const response = await fetch('/api/fileupload/upload', {
                        method: 'POST',
                        body: formData
                    });

                    if (!response.ok) {
                        const errorData = await response.json();
                        throw new Error(errorData.error || 'Upload failed');
                    }

                    const data = await response.json();

                    this.uploadedFiles.push({
                        fileName: data.fileName,
                        checksum: data.checksum,
                        tempFilePath: data.tempFilePath,
                        uniqueFileName: data.uniqueFileName,
                        fileSizeBytes: data.fileSizeBytes,
                        alreadyProcessed: data.alreadyProcessed,
                        processedFile: data.processedFile
                    });

                } catch (error) {
                    console.error('Upload error:', error);
                    this.showNotification(`Błąd uploadu ${file.name}: ${error.message}`, 'error');
                }
            }

            // Reset file input
            event.target.value = '';

            const processedCount = this.uploadedFiles.filter(f => f.alreadyProcessed).length;
            if (processedCount > 0) {
                this.showNotification(
                    `Dodano ${files.length} plików (${processedCount} już przetworzonych)`,
                    'warning'
                );
            } else {
                this.showNotification(`Dodano ${files.length} plików`, 'success');
            }
        },

        removeUploadedFile(index) {
            this.uploadedFiles.splice(index, 1);
        },

        async processUploadedFiles() {
            if (this.uploadedFiles.length === 0) {
                this.showNotification('Brak plików do przetworzenia!', 'error');
                return;
            }

            // Check if any files are already processed
            const processedFiles = this.uploadedFiles.filter(f => f.alreadyProcessed);

            if (processedFiles.length > 0) {
                // Show confirmation modal for first processed file
                this.processedFileToConfirm = processedFiles[0];
                this.pendingFilesToProcess = [...this.uploadedFiles];
                this.showProcessedFileModal = true;
                return;
            }

            // No processed files, proceed directly
            await this.startProcessingUploadedFiles();
        },

        async confirmProcessFile() {
            this.showProcessedFileModal = false;
            this.processedFileToConfirm = null;
            await this.startProcessingUploadedFiles();
        },

        cancelProcessFile() {
            this.showProcessedFileModal = false;
            this.processedFileToConfirm = null;
            this.pendingFilesToProcess = [];

            // Remove processed files from upload list
            this.uploadedFiles = this.uploadedFiles.filter(f => !f.alreadyProcessed);

            if (this.uploadedFiles.length > 0) {
                this.showNotification(
                    `Usunięto już przetworzone pliki. Pozostało ${this.uploadedFiles.length} plików.`,
                    'info'
                );
            }
        },

        async startProcessingUploadedFiles() {
            try {
                const filePaths = this.uploadedFiles.map(f => f.tempFilePath);

                const response = await fetch('/api/processing/process-uploaded', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ filePaths: filePaths })
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to start processing');
                }

                this.showNotification(
                    `Rozpoczęto przetwarzanie ${this.uploadedFiles.length} plików!`,
                    'success'
                );

                // Clear uploaded files list
                this.uploadedFiles = [];

                // Start monitoring
                this.startStatusMonitoring();

            } catch (error) {
                console.error('Error processing uploaded files:', error);
                this.showNotification('Błąd przetwarzania: ' + error.message, 'error');
            }
        },

        formatDate(dateString) {
            if (!dateString) return '';
            const date = new Date(dateString);
            return date.toLocaleString('pl-PL');
        },

        // ============== RECIPES ==============

        async loadRecipes() {
            try {
                const response = await fetch('/api/recipes');
                if (!response.ok) throw new Error('Failed to load recipes');

                this.recipes = await response.json();
                this.filterRecipes();
                this.updateRecipesWithoutImagesCount();
            } catch (error) {
                console.error('Error loading recipes:', error);
                this.showNotification('Błąd ładowania przepisów: ' + error.message, 'error');
            }
        },

        updateRecipesWithoutImagesCount() {
            this.recipesWithoutImages = this.recipes.filter(r => !r.imageUrl).length;
        },

        filterRecipes() {
            let filtered = this.recipes;

            // Apply search query filter
            if (this.searchQuery.trim()) {
                const query = this.searchQuery.toLowerCase();
                filtered = filtered.filter(recipe =>
                    recipe.name.toLowerCase().includes(query) ||
                    recipe.description.toLowerCase().includes(query) ||
                    this.getMealTypeName(recipe.mealType).toLowerCase().includes(query)
                );
            }

            // Apply meal type filter
            if (this.recipeFilters.mealTypes.length > 0) {
                filtered = filtered.filter(recipe =>
                    this.recipeFilters.mealTypes.includes(recipe.mealType)
                );
            }

            // Apply nutrition filters
            filtered = filtered.filter(recipe => {
                // Calories filter
                if (recipe.calories < this.recipeFilters.calories.min ||
                    recipe.calories > this.recipeFilters.calories.max) {
                    return false;
                }

                // Protein filter
                if (recipe.protein < this.recipeFilters.protein.min ||
                    recipe.protein > this.recipeFilters.protein.max) {
                    return false;
                }

                // Carbs filter
                if (recipe.carbohydrates < this.recipeFilters.carbs.min ||
                    recipe.carbohydrates > this.recipeFilters.carbs.max) {
                    return false;
                }

                // Fat filter
                if (recipe.fat < this.recipeFilters.fat.min ||
                    recipe.fat > this.recipeFilters.fat.max) {
                    return false;
                }

                return true;
            });

            // Apply sorting
            filtered = this.sortRecipes(filtered);

            this.filteredRecipes = filtered;
        },

        sortRecipes(recipes) {
            const field = this.recipeSorting.field;
            const direction = this.recipeSorting.direction;

            return recipes.sort((a, b) => {
                let aValue, bValue;

                if (field === 'name') {
                    aValue = a.name.toLowerCase();
                    bValue = b.name.toLowerCase();
                } else {
                    aValue = a[field] || 0;
                    bValue = b[field] || 0;
                }

                if (aValue < bValue) {
                    return direction === 'asc' ? -1 : 1;
                }
                if (aValue > bValue) {
                    return direction === 'asc' ? 1 : -1;
                }
                return 0;
            });
        },

        toggleSortDirection() {
            this.recipeSorting.direction = this.recipeSorting.direction === 'asc' ? 'desc' : 'asc';
            this.filterRecipes();
        },

        // Alias for filterRecipes() - called by range sliders
        applyRecipeFilters() {
            this.filterRecipes();
        },

        toggleMealTypeFilter(mealType) {
            const index = this.recipeFilters.mealTypes.indexOf(mealType);
            if (index > -1) {
                this.recipeFilters.mealTypes.splice(index, 1);
            } else {
                this.recipeFilters.mealTypes.push(mealType);
            }
            this.filterRecipes();
        },

        clearRecipeFilters() {
            this.recipeFilters = {
                mealTypes: [],
                calories: { min: 0, max: 3000 },
                protein: { min: 0, max: 200 },
                carbs: { min: 0, max: 300 },
                fat: { min: 0, max: 150 }
            };
            this.recipeSorting = {
                field: 'name',
                direction: 'asc'
            };
            this.searchQuery = '';
            this.filterRecipes();
        },

        hasActiveFilters() {
            return (
                this.recipeFilters.mealTypes.length > 0 ||
                this.recipeFilters.calories.min > 0 ||
                this.recipeFilters.calories.max < 3000 ||
                this.recipeFilters.protein.min > 0 ||
                this.recipeFilters.protein.max < 200 ||
                this.recipeFilters.carbs.min > 0 ||
                this.recipeFilters.carbs.max < 300 ||
                this.recipeFilters.fat.min > 0 ||
                this.recipeFilters.fat.max < 150 ||
                this.searchQuery.trim() !== ''
            );
        },

        getMealTypeName(mealType) {
            if (typeof mealType === 'number') {
                return MEAL_TYPE_NAMES[mealType] || mealType;
            }
            return MEAL_TYPE_NAMES[mealType] || mealType;
        },

        viewRecipeDetails(recipe) {
            this.selectedRecipe = recipe;
            this.showRecipeModal = true;
        },

        editRecipe(recipe) {
            this.openEditRecipeModal(recipe);
        },

        async deleteRecipe(id) {
            const recipe = this.recipes.find(r => r.id === id);
            if (!recipe) return;

            if (!confirm(`Czy na pewno chcesz usunąć przepis "${recipe.name}"?`)) {
                return;
            }

            try {
                const response = await fetch(`/api/recipes/${id}`, {
                    method: 'DELETE'
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to delete recipe');
                }

                this.showNotification('Przepis usunięty!', 'success');
                await this.loadRecipes();
            } catch (error) {
                console.error('Error deleting recipe:', error);
                this.showNotification('Błąd usuwania: ' + error.message, 'error');
            }
        },

        // ============== RECIPE IMAGES ==============

        selectImageForRecipe(recipeId) {
            this.currentRecipeIdForImage = recipeId;
            document.getElementById('recipeImageInput').click();
        },

        async uploadRecipeImage(event) {
            const file = event.target.files[0];
            if (!file) return;

            const recipeId = this.currentRecipeIdForImage;
            if (!recipeId) return;

            try {
                const formData = new FormData();
                formData.append('image', file);

                const response = await fetch(`/api/recipes/${recipeId}/image`, {
                    method: 'POST',
                    body: formData
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to upload image');
                }

                const result = await response.json();
                this.showNotification('Zdjęcie przesłane!', 'success');

                // Update recipe in local state
                const recipe = this.recipes.find(r => r.id === recipeId);
                if (recipe) {
                    recipe.imageUrl = result.imageUrl;
                }
                this.filterRecipes();

                // Clear file input
                event.target.value = '';
                this.currentRecipeIdForImage = null;
            } catch (error) {
                console.error('Error uploading image:', error);
                this.showNotification('Błąd uploadu: ' + error.message, 'error');
            }
        },

        async deleteRecipeImage(recipeId) {
            if (!confirm('Czy na pewno chcesz usunąć zdjęcie tego przepisu?')) {
                return;
            }

            try {
                const response = await fetch(`/api/recipes/${recipeId}/image`, {
                    method: 'DELETE'
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to delete image');
                }

                this.showNotification('Zdjęcie usunięte!', 'success');

                // Update recipe in local state
                const recipe = this.recipes.find(r => r.id === recipeId);
                if (recipe) {
                    recipe.imageUrl = null;
                    recipe.imagePath = null;
                }
                this.filterRecipes();
                this.updateRecipesWithoutImagesCount();
            } catch (error) {
                console.error('Error deleting image:', error);
                this.showNotification('Błąd usuwania: ' + error.message, 'error');
            }
        },

        // ============== AI IMAGE GENERATION ==============

        toggleRecipeSelection(recipeId) {
            const index = this.selectedRecipeIds.indexOf(recipeId);
            if (index === -1) {
                this.selectedRecipeIds.push(recipeId);
            } else {
                this.selectedRecipeIds.splice(index, 1);
            }
        },

        clearSelection() {
            this.selectedRecipeIds = [];
        },

        async generateImageForRecipe(recipeId) {
            try {
                this.isGeneratingImages = true;
                this.imageGenerationProgress = 'Generowanie obrazu...';

                const response = await fetch(`/api/images/generate/${recipeId}`, {
                    method: 'POST'
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to generate image');
                }

                const result = await response.json();
                this.showNotification(`✅ Obraz wygenerowany dla: ${result.recipeName}`, 'success');

                // Update recipe in local state
                const recipe = this.recipes.find(r => r.id === recipeId);
                if (recipe) {
                    recipe.imageUrl = result.imageUrl;
                }
                this.filterRecipes();
                this.updateRecipesWithoutImagesCount();
            } catch (error) {
                console.error('Error generating image:', error);
                this.showNotification('Błąd generowania: ' + error.message, 'error');
            } finally {
                this.isGeneratingImages = false;
                this.imageGenerationProgress = '';
            }
        },

        async generateImagesForSelected() {
            if (this.selectedRecipeIds.length === 0) {
                this.showNotification('Nie zaznaczono żadnych przepisów', 'error');
                return;
            }

            if (!confirm(`Czy na pewno chcesz wygenerować obrazy dla ${this.selectedRecipeIds.length} zaznaczonych przepisów?`)) {
                return;
            }

            try {
                this.isGeneratingImages = true;
                this.imageGenerationProgress = `Generowanie 0/${this.selectedRecipeIds.length}...`;

                const response = await fetch('/api/images/generate-batch', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        recipeIds: this.selectedRecipeIds
                    })
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to generate images');
                }

                const result = await response.json();
                this.showNotification(`✅ Wygenerowano ${result.successful}/${result.totalRequested} obrazów`, 'success');

                // Update recipes in local state
                result.results.forEach(res => {
                    if (res.success) {
                        const recipe = this.recipes.find(r => r.id === res.recipeId);
                        if (recipe) {
                            recipe.imageUrl = res.imageUrl;
                        }
                    }
                });

                this.filterRecipes();
                this.updateRecipesWithoutImagesCount();
                this.clearSelection();
            } catch (error) {
                console.error('Error generating images:', error);
                this.showNotification('Błąd generowania: ' + error.message, 'error');
            } finally {
                this.isGeneratingImages = false;
                this.imageGenerationProgress = '';
            }
        },

        async generateImagesForAllMissing() {
            const count = this.recipesWithoutImages;
            if (count === 0) {
                this.showNotification('Wszystkie przepisy mają już zdjęcia!', 'error');
                return;
            }

            if (!confirm(`Czy na pewno chcesz wygenerować obrazy dla wszystkich ${count} przepisów bez zdjęć? To może potrwać kilka minut.`)) {
                return;
            }

            try {
                this.isGeneratingImages = true;
                this.imageGenerationProgress = `Generowanie obrazów dla ${count} przepisów...`;

                const response = await fetch('/api/images/generate-all-missing', {
                    method: 'POST'
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to generate images');
                }

                const result = await response.json();
                this.showNotification(`✅ Wygenerowano ${result.successful}/${result.totalRequested} obrazów`, 'success');

                // Reload recipes to get updated images
                await this.loadRecipes();
            } catch (error) {
                console.error('Error generating images:', error);
                this.showNotification('Błąd generowania: ' + error.message, 'error');
            } finally {
                this.isGeneratingImages = false;
                this.imageGenerationProgress = '';
            }
        },

        // ============== IMAGE SETTINGS ==============

        async loadImageSettings() {
            try {
                const response = await fetch('/api/imagesettings');
                if (!response.ok) throw new Error('Failed to load image settings');
                this.imageSettings = await response.json();
            } catch (error) {
                console.error('Error loading image settings:', error);
                // Use defaults if loading fails
            }
        },

        async saveImageSettings() {
            this.imageSettingsSaving = true;
            try {
                const response = await fetch('/api/imagesettings', {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        provider: this.imageSettings.provider,
                        openAI: this.imageSettings.openAI,
                        gemini: this.imageSettings.gemini
                    })
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to save settings');
                }

                await this.loadImageSettings();
                this.showNotification('✅ Ustawienia zapisane pomyślnie', 'success');
            } catch (error) {
                console.error('Error saving image settings:', error);
                this.showNotification('❌ Błąd zapisu: ' + error.message, 'error');
            } finally {
                this.imageSettingsSaving = false;
            }
        },

        async switchImageProvider(provider) {
            try {
                const response = await fetch('/api/imagesettings/switch-provider', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ provider })
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to switch provider');
                }

                this.imageSettings.provider = provider;
                this.showNotification(`✅ Przełączono na ${provider}`, 'success');
            } catch (error) {
                console.error('Error switching provider:', error);
                this.showNotification('❌ Błąd: ' + error.message, 'error');
            }
        },

        async testImageGeneration() {
            this.imageSettingsTestRunning = true;
            try {
                const response = await fetch('/api/imagesettings/test', {
                    method: 'POST'
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Test failed');
                }

                const result = await response.json();
                this.showNotification(`✅ Test zakończony sukcesem! Provider: ${result.provider}`, 'success');
            } catch (error) {
                console.error('Error testing image generation:', error);
                this.showNotification('❌ Test nieudany: ' + error.message, 'error');
            } finally {
                this.imageSettingsTestRunning = false;
            }
        },

        async saveModelSettings(provider) {
            try {
                const payload = {
                    [provider]: {
                        apiKey: '***', // Don't update API key
                        model: this.imageSettings[provider].model
                    }
                };

                const response = await fetch('/api/imagesettings', {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to save model');
                }

                // Show brief success indicator
                const refName = provider + 'ModelSaved';
                if (this.$refs[refName]) {
                    this.$refs[refName].style.display = 'block';
                    setTimeout(() => {
                        this.$refs[refName].style.display = 'none';
                    }, 2000);
                }

                console.log(`✅ Zapisano model ${provider}: ${this.imageSettings[provider].model}`);
            } catch (error) {
                console.error('Error saving model:', error);
                this.showNotification('❌ Błąd zapisu modelu: ' + error.message, 'error');
            }
        },

        // ============== TODOIST INTEGRATION ==============

        async loadTodoistSettings() {
            try {
                const response = await fetch('/api/todoist/settings');
                if (!response.ok) throw new Error('Failed to load Todoist settings');
                this.todoistSettings = await response.json();
            } catch (error) {
                console.error('Error loading Todoist settings:', error);
                // Use defaults if loading fails
            }
        },

        async saveTodoistSettings() {
            this.todoistSettingsSaving = true;
            try {
                const response = await fetch('/api/todoist/settings', {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        apiKey: this.todoistSettings.apiKey
                    })
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to save Todoist settings');
                }

                await this.loadTodoistSettings();
                this.showNotification('✅ Klucz API Todoist zapisany pomyślnie', 'success');
            } catch (error) {
                console.error('Error saving Todoist settings:', error);
                this.showNotification('❌ Błąd zapisu: ' + error.message, 'error');
            } finally {
                this.todoistSettingsSaving = false;
            }
        },

        async testTodoistConnection() {
            this.todoistTestRunning = true;
            try {
                const response = await fetch('/api/todoist/test', {
                    method: 'POST'
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Test failed');
                }

                const result = await response.json();
                this.showNotification(`✅ Test zakończony sukcesem! Utworzono projekt testowy.`, 'success');
            } catch (error) {
                console.error('Error testing Todoist connection:', error);
                this.showNotification('❌ Test nieudany: ' + error.message, 'error');
            } finally {
                this.todoistTestRunning = false;
            }
        },

        async exportToTodoist() {
            if (!this.selectedPlan || !this.selectedPlan.id) {
                this.showNotification('❌ Nie wybrano planu', 'error');
                return;
            }

            if (!this.todoistSettings.isConfigured) {
                this.showNotification('❌ Skonfiguruj klucz API Todoist w Ustawieniach', 'error');
                return;
            }

            this.isExportingToTodoist = true;
            try {
                const response = await fetch(`/api/todoist/export/${this.selectedPlan.id}`, {
                    method: 'POST'
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Export failed');
                }

                const result = await response.json();
                this.showNotification(`✅ Wyeksportowano do Todoist! Utworzono ${result.tasksCreated} zadań.`, 'success');

                // Optionally open Todoist project in new tab
                if (result.projectUrl) {
                    console.log(`📋 Link do projektu Todoist: ${result.projectUrl}`);
                }
            } catch (error) {
                console.error('Error exporting to Todoist:', error);
                this.showNotification('❌ Błąd exportu: ' + error.message, 'error');
            } finally {
                this.isExportingToTodoist = false;
            }
        },

        // ============== AI MODEL SETTINGS ==============

        async loadAIModelSettings() {
            try {
                const response = await fetch('/api/aimodelsettings');
                if (!response.ok) throw new Error('Failed to load AI model settings');
                this.aiModelSettings = await response.json();
            } catch (error) {
                console.error('Error loading AI model settings:', error);
                // Use defaults if loading fails
            }
        },

        async saveAIModelSettings(settingType) {
            try {
                const payload = {};

                if (settingType === 'recipeScaling') {
                    payload.recipeScaling = {
                        model: this.aiModelSettings.recipeScaling.model
                    };
                } else if (settingType === 'dessertPlanning') {
                    payload.dessertPlanning = {
                        model: this.aiModelSettings.dessertPlanning.model
                    };
                }

                const response = await fetch('/api/aimodelsettings', {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to save AI model settings');
                }

                // Show brief success indicator
                const refName = settingType + 'ModelSaved';
                if (this.$refs[refName]) {
                    this.$refs[refName].style.display = 'block';
                    setTimeout(() => {
                        this.$refs[refName].style.display = 'none';
                    }, 2000);
                }
            } catch (error) {
                console.error('Error saving AI model settings:', error);
                this.showNotification('❌ Błąd zapisu: ' + error.message, 'error');
            }
        },

        // ============== NOTIFICATIONS ==============

        // ============== MEAL PLANNER ==============

        async loadMealPlans() {
            try {
                const response = await fetch('/api/mealplans');
                if (!response.ok) throw new Error('Failed to load meal plans');
                this.mealPlans = await response.json();
            } catch (error) {
                console.error('Error loading meal plans:', error);
            }
        },

        async selectMealPlan(planId) {
            try {
                const response = await fetch(`/api/mealplans/${planId}`);
                if (!response.ok) throw new Error('Failed to load meal plan');
                this.selectedPlan = await response.json();

                // Initialize recipes for filtering
                this.filteredMealPlanRecipes = this.recipes;
            } catch (error) {
                console.error('Error loading meal plan:', error);
                this.showNotification('Błąd ładowania planu: ' + error.message, 'error');
            }
        },

        calculateDaysFromDates() {
            if (this.createPlanForm.startDate && this.createPlanForm.endDate) {
                const start = new Date(this.createPlanForm.startDate);
                const end = new Date(this.createPlanForm.endDate);

                // Calculate difference in milliseconds and convert to days
                const diffTime = Math.abs(end - start);
                const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24)) + 1; // +1 to include both start and end day

                this.createPlanForm.numberOfDays = diffDays;
            }
        },

        async createMealPlan() {
            try {
                const response = await fetch('/api/mealplans', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(this.createPlanForm)
                });

                if (!response.ok) throw new Error('Failed to create meal plan');

                const newPlan = await response.json();
                this.showNotification('Plan utworzony pomyślnie!', 'success');
                this.showCreatePlanModal = false;

                // Reset form
                this.createPlanForm = {
                    name: '',
                    startDate: '',
                    endDate: '',
                    numberOfDays: 7
                };

                // Reload plans and select the new one
                await this.loadMealPlans();
                await this.selectMealPlan(newPlan.id);
            } catch (error) {
                console.error('Error creating meal plan:', error);
                this.showNotification('Błąd tworzenia planu: ' + error.message, 'error');
            }
        },

        async deleteMealPlan(planId) {
            if (!confirm('Czy na pewno chcesz usunąć ten plan?')) return;

            try {
                const response = await fetch(`/api/mealplans/${planId}`, {
                    method: 'DELETE'
                });

                if (!response.ok) throw new Error('Failed to delete meal plan');

                this.showNotification('Plan usunięty', 'success');

                // Clear selection if this was the selected plan
                if (this.selectedPlan?.id === planId) {
                    this.selectedPlan = null;
                }

                await this.loadMealPlans();
            } catch (error) {
                console.error('Error deleting meal plan:', error);
                this.showNotification('Błąd usuwania planu: ' + error.message, 'error');
            }
        },

        startEditingPlanName() {
            if (!this.selectedPlan) return;
            this.planNameEdit = this.selectedPlan.name;
            this.editingPlanName = true;
            // Focus input after render
            this.$nextTick(() => {
                this.$refs.planNameInput?.focus();
                this.$refs.planNameInput?.select();
            });
        },

        cancelEditingPlanName() {
            this.editingPlanName = false;
            this.planNameEdit = '';
        },

        async savePlanName() {
            if (!this.selectedPlan || !this.planNameEdit.trim()) {
                this.cancelEditingPlanName();
                return;
            }

            try {
                const response = await fetch(`/api/mealplans/${this.selectedPlan.id}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        name: this.planNameEdit.trim(),
                        startDate: this.selectedPlan.startDate,
                        endDate: this.selectedPlan.endDate
                    })
                });

                if (!response.ok) throw new Error('Failed to update plan name');

                // Update local state
                this.selectedPlan.name = this.planNameEdit.trim();

                // Update in the list
                const planIndex = this.mealPlans.findIndex(p => p.id === this.selectedPlan.id);
                if (planIndex !== -1) {
                    this.mealPlans[planIndex].name = this.planNameEdit.trim();
                }

                this.showNotification('Nazwa planu zaktualizowana', 'success');
                this.cancelEditingPlanName();
            } catch (error) {
                console.error('Error updating plan name:', error);
                this.showNotification('Błąd aktualizacji nazwy: ' + error.message, 'error');
            }
        },

        // Person management
        async addPerson() {
            if (!this.selectedPlan) return;
            if (!this.newPerson.name.trim()) {
                this.showNotification('Podaj imię osoby', 'error');
                return;
            }
            if (!this.newPerson.targetCalories || this.newPerson.targetCalories < 1000 || this.newPerson.targetCalories > 5000) {
                this.showNotification('Cel kaloryczny musi być w zakresie 1000-5000 kcal', 'error');
                return;
            }

            try {
                const response = await fetch(`/api/mealplans/${this.selectedPlan.id}/persons`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        name: this.newPerson.name.trim(),
                        targetCalories: this.newPerson.targetCalories
                    })
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to add person');
                }

                this.showNotification('Osoba dodana do planu', 'success');

                // Reset form
                this.newPerson.name = '';
                this.newPerson.targetCalories = 2000;

                // Reload plan to get updated persons list
                await this.selectMealPlan(this.selectedPlan.id);
            } catch (error) {
                console.error('Error adding person:', error);
                this.showNotification('Błąd dodawania osoby: ' + error.message, 'error');
            }
        },

        async updatePerson(person) {
            if (!this.selectedPlan) return;
            if (!person.name.trim()) {
                this.showNotification('Podaj imię osoby', 'error');
                return;
            }
            if (!person.targetCalories || person.targetCalories < 1000 || person.targetCalories > 5000) {
                this.showNotification('Cel kaloryczny musi być w zakresie 1000-5000 kcal', 'error');
                return;
            }

            try {
                const response = await fetch(`/api/mealplans/${this.selectedPlan.id}/persons/${person.id}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        name: person.name.trim(),
                        targetCalories: person.targetCalories
                    })
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to update person');
                }

                this.showNotification('Osoba zaktualizowana', 'success');

                // Exit edit mode
                person.editing = false;

                // Reload plan to get updated data
                await this.selectMealPlan(this.selectedPlan.id);
            } catch (error) {
                console.error('Error updating person:', error);
                this.showNotification('Błąd aktualizacji osoby: ' + error.message, 'error');
            }
        },

        async deletePerson(personId) {
            if (!this.selectedPlan) return;
            if (!confirm('Czy na pewno chcesz usunąć tę osobę? Wszystkie przeskalowane przepisy dla tej osoby zostaną usunięte.')) return;

            try {
                const response = await fetch(`/api/mealplans/${this.selectedPlan.id}/persons/${personId}`, {
                    method: 'DELETE'
                });

                if (!response.ok) throw new Error('Failed to delete person');

                this.showNotification('Osoba usunięta', 'success');

                // Reload plan to update persons list
                await this.selectMealPlan(this.selectedPlan.id);
            } catch (error) {
                console.error('Error deleting person:', error);
                this.showNotification('Błąd usuwania osoby: ' + error.message, 'error');
            }
        },

        async scaleRecipeForPlan(entryId) {
            if (!this.selectedPlan) return;
            if (!this.selectedPlan.persons || this.selectedPlan.persons.length === 0) {
                this.showNotification('Dodaj osoby do planu przed skalowaniem', 'error');
                return;
            }

            // Find the entry and mark as scaling
            let entry = null;
            for (const day of this.selectedPlan.days || []) {
                if (day.entries) {
                    entry = day.entries.find(e => e.id === entryId);
                    if (entry) break;
                }
            }

            if (!entry) return;

            entry.scaling = true;

            try {
                const response = await fetch(`/api/mealplans/${this.selectedPlan.id}/entries/${entryId}/scale`, {
                    method: 'POST'
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Failed to scale recipe');
                }

                const result = await response.json();

                if (result.isDessert) {
                    this.showNotification(`Deser przeskalowany: ${result.dessertPlan.explanation}`, 'success');
                } else {
                    this.showNotification(`Przepis przeskalowany dla ${result.scaledRecipes} osób`, 'success');
                }

                // Mark entry as scaled
                entry.scaled = true;
                entry.scaling = false;

                // Optionally reload plan to get full data
                // await this.selectMealPlan(this.selectedPlan.id);
            } catch (error) {
                console.error('Error scaling recipe:', error);
                this.showNotification('Błąd skalowania: ' + error.message, 'error');
                entry.scaling = false;
            }
        },

        async dropRecipeOnDay(dayId, event) {
            event.preventDefault();

            if (!this.draggedRecipe) return;

            try {
                const response = await fetch(`/api/mealplans/${this.selectedPlan.id}/days/${dayId}/entries`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        recipeId: this.draggedRecipe.id,
                        mealType: this.draggedRecipe.mealType,
                        order: 0
                    })
                });

                if (!response.ok) throw new Error('Failed to add recipe to day');

                this.showNotification('Przepis dodany do planu', 'success');

                // Reload the selected plan to show the change
                await this.selectMealPlan(this.selectedPlan.id);
            } catch (error) {
                console.error('Error adding recipe to day:', error);
                this.showNotification('Błąd dodawania przepisu: ' + error.message, 'error');
            }
        },

        async removeRecipeFromDay(dayId, entryId) {
            try {
                const response = await fetch(`/api/mealplans/${this.selectedPlan.id}/days/${dayId}/entries/${entryId}`, {
                    method: 'DELETE'
                });

                if (!response.ok) throw new Error('Failed to remove recipe');

                this.showNotification('Przepis usunięty z planu', 'success');

                // Reload the selected plan
                await this.selectMealPlan(this.selectedPlan.id);
            } catch (error) {
                console.error('Error removing recipe:', error);
                this.showNotification('Błąd usuwania przepisu: ' + error.message, 'error');
            }
        },

        async moveEntryUp(dayId, entryId, currentIndex) {
            if (currentIndex === 0) return; // Already at the top
            await this.updateEntryOrder(dayId, entryId, currentIndex - 1);
        },

        async moveEntryDown(dayId, entryId, currentIndex, totalEntries) {
            if (currentIndex >= totalEntries - 1) return; // Already at the bottom
            await this.updateEntryOrder(dayId, entryId, currentIndex + 1);
        },

        async updateEntryOrder(dayId, entryId, newOrder) {
            try {
                const response = await fetch(`/api/mealplans/${this.selectedPlan.id}/days/${dayId}/entries/${entryId}/order`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ newOrder })
                });

                if (!response.ok) throw new Error('Failed to update order');

                // Reload the selected plan to show updated order
                await this.selectMealPlan(this.selectedPlan.id);
            } catch (error) {
                console.error('Error updating order:', error);
                this.showNotification('Błąd zmiany kolejności: ' + error.message, 'error');
            }
        },

        startDragEntry(entry, dayId) {
            this.draggedEntry = entry;
            this.draggedFromDayId = dayId;
        },

        endDragEntry() {
            this.draggedEntry = null;
            this.draggedFromDayId = null;
        },

        async dropEntryOnDay(targetDayId, event) {
            event.preventDefault();

            if (!this.draggedEntry) return;

            // If dropping on the same day, do nothing
            if (this.draggedFromDayId === targetDayId) {
                this.endDragEntry();
                return;
            }

            try {
                // First, add the recipe to the target day
                const addResponse = await fetch(`/api/mealplans/${this.selectedPlan.id}/days/${targetDayId}/entries`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        recipeId: this.draggedEntry.recipe.id,
                        mealType: this.draggedEntry.mealType,
                        order: 0
                    })
                });

                if (!addResponse.ok) throw new Error('Failed to add recipe to day');

                // Then, remove from the original day
                const removeResponse = await fetch(`/api/mealplans/${this.selectedPlan.id}/days/${this.draggedFromDayId}/entries/${this.draggedEntry.id}`, {
                    method: 'DELETE'
                });

                if (!removeResponse.ok) throw new Error('Failed to remove recipe from original day');

                this.showNotification('Przepis przeniesiony!', 'success');

                // Reload the selected plan
                await this.selectMealPlan(this.selectedPlan.id);
            } catch (error) {
                console.error('Error moving recipe:', error);
                this.showNotification('Błąd przenoszenia przepisu: ' + error.message, 'error');
            } finally {
                this.endDragEntry();
            }
        },

        async autoGenerateMealPlan() {
            this.showAutoGenerateModal = true;
        },

        async confirmAutoGenerate() {
            this.isGeneratingPlan = true;
            this.showAutoGenerateModal = false;

            try {
                const response = await fetch(`/api/mealplans/${this.selectedPlan.id}/auto-generate`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(this.autoGenerateForm)
                });

                if (!response.ok) {
                    const errorData = await response.json();
                    throw new Error(errorData.error || 'Failed to auto-generate meal plan');
                }

                const result = await response.json();

                // Show success message - use backend message which includes scaling info
                this.showNotification(result.message || `Wygenerowano ${result.addedCount} przepisów!`, 'success');

                // Show warnings if any
                if (result.warnings && result.warnings.length > 0) {
                    // Display warnings as separate notifications
                    setTimeout(() => {
                        result.warnings.forEach((warning, index) => {
                            setTimeout(() => {
                                this.showNotification(warning, 'error');
                            }, index * 500); // Stagger notifications
                        });
                    }, 1000);
                }

                // Reload the plan
                await this.selectMealPlan(this.selectedPlan.id);
            } catch (error) {
                console.error('Error auto-generating meal plan:', error);
                this.showNotification('Błąd auto-generowania: ' + error.message, 'error');
            } finally {
                this.isGeneratingPlan = false;
            }
        },

        async generateShoppingList(forceRegenerate = false) {
            this.isGeneratingShoppingList = true;

            try {
                // First, check if shopping list already exists (unless forcing regenerate)
                if (!forceRegenerate) {
                    const checkResponse = await fetch(`/api/mealplans/${this.selectedPlan.id}/shopping-list`);
                    if (checkResponse.ok) {
                        // Shopping list exists, show it
                        this.shoppingList = await checkResponse.json();
                        this.showShoppingListModal = true;
                        this.showNotification('Załadowano zapisaną listę zakupów', 'success');
                        this.isGeneratingShoppingList = false;
                        return;
                    }
                }

                // Generate new shopping list
                const response = await fetch(`/api/mealplans/${this.selectedPlan.id}/shopping-list`, {
                    method: 'POST'
                });

                if (!response.ok) throw new Error('Failed to generate shopping list');

                this.shoppingList = await response.json();
                this.showShoppingListModal = true;
                this.showNotification(forceRegenerate ? 'Lista zakupów zaktualizowana!' : 'Lista zakupów wygenerowana!', 'success');
            } catch (error) {
                console.error('Error generating shopping list:', error);
                this.showNotification('Błąd generowania listy: ' + error.message, 'error');
            } finally {
                this.isGeneratingShoppingList = false;
            }
        },

        filterRecipesForMealPlan() {
            let filtered = this.recipes;

            // Apply search query
            if (this.recipeSearchQuery) {
                const query = this.recipeSearchQuery.toLowerCase();
                filtered = filtered.filter(r =>
                    r.name.toLowerCase().includes(query) ||
                    r.description?.toLowerCase().includes(query)
                );
            }

            // Apply meal type filter
            if (this.filterMealType !== null) {
                filtered = filtered.filter(r => r.mealType === this.filterMealType);
            }

            this.filteredMealPlanRecipes = filtered;
        },

        openEditRecipeModal(recipe) {
            // Create a deep copy to avoid modifying the original
            this.editingRecipe = {
                id: recipe.id,
                name: recipe.name,
                description: recipe.description || '',
                ingredients: recipe.ingredients || '',
                instructions: recipe.instructions || '',
                calories: recipe.calories || 0,
                protein: recipe.protein || 0,
                carbohydrates: recipe.carbohydrates || 0,
                fat: recipe.fat || 0,
                mealType: recipe.mealType,
                servings: recipe.servings || null
            };
            this.showEditRecipeModal = true;
        },

        async saveRecipeEdit() {
            try {
                const response = await fetch(`/api/recipes/${this.editingRecipe.id}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(this.editingRecipe)
                });

                if (!response.ok) throw new Error('Failed to update recipe');

                this.showNotification('Przepis zaktualizowany!', 'success');
                this.showEditRecipeModal = false;

                // Reload the current plan to show updated recipe
                if (this.selectedPlan) {
                    await this.selectMealPlan(this.selectedPlan.id);
                }

                // Also reload all recipes
                await this.loadRecipes();
            } catch (error) {
                console.error('Error updating recipe:', error);
                this.showNotification('Błąd aktualizacji przepisu: ' + error.message, 'error');
            }
        },

        openCreateRecipeModal() {
            // Initialize new recipe with default values
            this.newRecipe = {
                name: '',
                description: '',
                ingredients: '',
                instructions: '',
                calories: 0,
                protein: 0,
                carbohydrates: 0,
                fat: 0,
                mealType: 0, // Śniadanie
                servings: null
            };
            this.showCreateRecipeModal = true;
        },

        async saveNewRecipe() {
            try {
                const response = await fetch('/api/recipes', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(this.newRecipe)
                });

                if (!response.ok) throw new Error('Failed to create recipe');

                const createdRecipe = await response.json();

                this.showNotification(`Przepis "${createdRecipe.name}" został dodany!`, 'success');
                this.showCreateRecipeModal = false;

                // Reload all recipes
                await this.loadRecipes();

                // If we're in meal planner, reload the available recipes
                if (this.currentTab === 'meal-planner') {
                    this.filterMealPlanRecipes();
                }
            } catch (error) {
                console.error('Error creating recipe:', error);
                this.showNotification('Błąd tworzenia przepisu: ' + error.message, 'error');
            }
        },

        getDayOfWeekName(dayOfWeek) {
            const days = {
                0: 'Poniedziałek',
                1: 'Wtorek',
                2: 'Środa',
                3: 'Czwartek',
                4: 'Piątek',
                5: 'Sobota',
                6: 'Niedziela'
            };
            return days[dayOfWeek] || 'Unknown';
        },

        formatDate(dateString) {
            if (!dateString) return '';
            const date = new Date(dateString);
            return date.toLocaleDateString('pl-PL', { year: 'numeric', month: '2-digit', day: '2-digit' });
        },

        calculateDaysBetween(startDate, endDate) {
            if (!startDate || !endDate) return 0;
            const start = new Date(startDate);
            const end = new Date(endDate);
            const diffTime = Math.abs(end - start);
            const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24)) + 1; // +1 to include both start and end day
            return diffDays;
        },

        getMealTypeName(mealType) {
            return MEAL_TYPE_NAMES[mealType] || 'Unknown';
        },

        getDayTotalCalories(day) {
            if (!day || !day.entries || day.entries.length === 0) return 0;
            return day.entries.reduce((total, entry) => {
                return total + (entry.recipe?.calories || 0);
            }, 0);
        },

        getDayTotalMacros(day) {
            if (!day || !day.entries || day.entries.length === 0) {
                return { protein: 0, carbohydrates: 0, fat: 0 };
            }
            return day.entries.reduce((totals, entry) => {
                return {
                    protein: totals.protein + (entry.recipe?.protein || 0),
                    carbohydrates: totals.carbohydrates + (entry.recipe?.carbohydrates || 0),
                    fat: totals.fat + (entry.recipe?.fat || 0)
                };
            }, { protein: 0, carbohydrates: 0, fat: 0 });
        },

        getUniqueCategories(items) {
            if (!items) return [];
            return [...new Set(items.map(item => item.category))];
        },

        getItemsByCategory(items, category) {
            if (!items) return [];
            return items.filter(item => item.category === category);
        },

        printMealPlanTable() {
            if (!this.selectedPlan) {
                this.showNotification('Brak wybranego planu', 'error');
                return;
            }

            const printWindow = window.open(`/api/print/meal-plan/${this.selectedPlan.id}`, '_blank');
            if (printWindow) {
                printWindow.addEventListener('load', () => {
                    setTimeout(() => printWindow.print(), 250);
                });
            }
        },

        printMealPlanFull() {
            if (!this.selectedPlan) {
                this.showNotification('Brak wybranego planu', 'error');
                return;
            }

            const printWindow = window.open(`/api/print/meal-plan/${this.selectedPlan.id}/full`, '_blank');
            if (printWindow) {
                printWindow.addEventListener('load', () => {
                    setTimeout(() => printWindow.print(), 250);
                });
            }
        },

        printShoppingList() {
            if (!this.selectedPlan) {
                this.showNotification('Brak wybranego planu', 'error');
                return;
            }

            // Check if shopping list exists
            fetch(`/api/mealplans/${this.selectedPlan.id}/shopping-list`)
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Brak listy zakupowej. Wygeneruj ją najpierw.');
                    }
                    return response.json();
                })
                .then(() => {
                    const printWindow = window.open(`/api/print/shopping-list/${this.selectedPlan.id}`, '_blank');
                    if (printWindow) {
                        printWindow.addEventListener('load', () => {
                            setTimeout(() => printWindow.print(), 250);
                        });
                    }
                })
                .catch(error => {
                    this.showNotification(error.message, 'error');
                });
        },

        printMealPlanForPerson(personId) {
            if (!this.selectedPlan) {
                this.showNotification('Brak wybranego planu', 'error');
                return;
            }

            const person = this.selectedPlan.persons?.find(p => p.id === personId);
            if (!person) {
                this.showNotification('Nie znaleziono osoby', 'error');
                return;
            }

            const printWindow = window.open(`/api/print/meal-plan/${this.selectedPlan.id}/person/${personId}`, '_blank');
            if (printWindow) {
                printWindow.addEventListener('load', () => {
                    setTimeout(() => printWindow.print(), 250);
                });
            }
        },

        // ============== NOTIFICATIONS ==============

        showNotification(message, type = 'success') {
            const notification = { message, type };
            this.notifications.push(notification);

            setTimeout(() => {
                const index = this.notifications.indexOf(notification);
                if (index > -1) {
                    this.notifications.splice(index, 1);
                }
            }, 5000);
        }
    };
}

// Watch for tab changes and save to localStorage
document.addEventListener('alpine:initialized', () => {
    const app = Alpine.$data(document.body);

    // Watch for tab changes
    let lastTab = app.currentTab;
    setInterval(() => {
        if (app.currentTab !== lastTab) {
            localStorage.setItem('selectedTab', app.currentTab);
            lastTab = app.currentTab;
        }
    }, 100);
});
