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
        currentTab: 'providers',

        // AI Providers
        providers: [],
        activeProvider: null,
        showProviderModal: false,
        editingProvider: null,
        providerForm: {
            name: '',
            apiKey: '',
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

        // Recipes
        recipes: [],
        filteredRecipes: [],
        searchQuery: '',
        selectedRecipe: null,
        showRecipeModal: false,
        currentRecipeIdForImage: null,

        // Notifications
        notifications: [],

        // Initialization
        async init() {
            await this.loadProviders();
            await this.loadActiveProvider();
            await this.loadRecipes();
            await this.loadCurrentDirectory();

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
                apiKey: '',
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
                apiKey: provider.apiKey === '***' ? '' : provider.apiKey,
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
                    const updateData = {
                        name: this.providerForm.name,
                        model: this.providerForm.model,
                        priority: parseInt(this.providerForm.priority),
                        maxPagesPerChunk: parseInt(this.providerForm.maxPagesPerChunk),
                        supportsDirectPDF: this.providerForm.supportsDirectPDF,
                        isActive: this.providerForm.isActive
                    };

                    // Only include API key if it was changed
                    if (this.providerForm.apiKey && this.providerForm.apiKey !== '***') {
                        updateData.apiKey = this.providerForm.apiKey;
                    }

                    response = await fetch(`/api/aiproviders/${this.editingProvider.id}`, {
                        method: 'PUT',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(updateData)
                    });
                } else {
                    // Create new provider
                    response = await fetch('/api/aiproviders', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            name: this.providerForm.name,
                            apiKey: this.providerForm.apiKey,
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
                this.pdfFiles = data.files || [];
                this.showNotification(`Znaleziono ${this.pdfFiles.length} plików`, 'success');
            } catch (error) {
                console.error('Error loading files:', error);
                this.showNotification('Błąd ładowania plików: ' + error.message, 'error');
            }
        },

        selectAllFiles() {
            this.selectedPdfFiles = [...this.pdfFiles];
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

                    if (!status.isRunning && this.statusInterval) {
                        clearInterval(this.statusInterval);
                        this.statusInterval = null;

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
            }, 5000); // Check every 5 seconds
        },

        // ============== RECIPES ==============

        async loadRecipes() {
            try {
                const response = await fetch('/api/recipes');
                if (!response.ok) throw new Error('Failed to load recipes');

                this.recipes = await response.json();
                this.filterRecipes();
            } catch (error) {
                console.error('Error loading recipes:', error);
                this.showNotification('Błąd ładowania przepisów: ' + error.message, 'error');
            }
        },

        filterRecipes() {
            if (!this.searchQuery.trim()) {
                this.filteredRecipes = this.recipes;
                return;
            }

            const query = this.searchQuery.toLowerCase();
            this.filteredRecipes = this.recipes.filter(recipe =>
                recipe.name.toLowerCase().includes(query) ||
                recipe.description.toLowerCase().includes(query) ||
                this.getMealTypeName(recipe.mealType).toLowerCase().includes(query)
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
            // TODO: Implement recipe editing
            this.showNotification('Edycja przepisów będzie dostępna wkrótce', 'error');
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
            } catch (error) {
                console.error('Error deleting image:', error);
                this.showNotification('Błąd usuwania: ' + error.message, 'error');
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
