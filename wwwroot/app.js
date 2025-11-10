// Recipe AI Helper - Enhanced Version
// Mock data - w produkcji to łączyłoby się z backendem .NET przez API
let recipesDatabase = [];
let currentWeeklyPlan = {};
let availableFiles = [];
let selectedFiles = [];

// Configuration
const TODOIST_API_URL = 'https://api.todoist.com/rest/v2/tasks';
const PDF_FOLDER = 'C:\\Users\\Karolina\\Downloads\\Dieta'; // Domyślny folder
let currentAIModel = 'gpt-5-nano-2025-08-07'; // Default model

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    // Set default date to today
    document.getElementById('startDate').valueAsDate = new Date();

    // AI Model Configuration
    document.getElementById('aiModelSelect').addEventListener('change', onModelSelectChange);
    document.getElementById('saveModelBtn').addEventListener('click', saveModelConfiguration);
    loadModelConfiguration();

    // PDF File Management
    document.getElementById('loadFilesBtn').addEventListener('click', loadPdfFiles);
    document.getElementById('selectAllBtn').addEventListener('click', selectAllFiles);
    document.getElementById('deselectAllBtn').addEventListener('click', deselectAllFiles);
    document.getElementById('processSelectedBtn').addEventListener('click', processSelectedFiles);

    // Database Management
    document.getElementById('loadDatabaseBtn').addEventListener('click', loadDatabase);
    document.getElementById('refreshDbBtn').addEventListener('click', refreshDatabase);
    document.getElementById('searchDb').addEventListener('input', searchDatabase);

    // Meal Planning
    document.getElementById('generateDailyBtn').addEventListener('click', generateDailyPlan);
    document.getElementById('generateWeeklyBtn').addEventListener('click', generateWeeklyPlan);
    document.getElementById('printWeeklyBtn').addEventListener('click', printWeeklyPlan);

    // Shopping List
    document.getElementById('generateShoppingListBtn').addEventListener('click', generateShoppingList);
    document.getElementById('exportTodoistBtn').addEventListener('click', exportToTodoist);

    // Modal
    document.querySelector('.close').addEventListener('click', closeEditModal);
    document.getElementById('editForm').addEventListener('submit', saveRecipeEdit);

    // Load initial data
    loadRecipesFromDatabase();
});

// ============== AI MODEL CONFIGURATION ==============

const MODEL_INFO = {
    'gpt-5-nano-2025-08-07': {
        name: 'gpt-5-nano-2025-08-07',
        context: '400,000 tokenów',
        output: '128,000 tokenów',
        reasoning: true,
        pages: '~30-40 stron PDF na raz',
        description: 'Najnowszy model z rozszerzonym oknem kontekstu'
    },
    'gpt-4o': {
        name: 'gpt-4o',
        context: '128,000 tokenów',
        output: '16,384 tokenów',
        reasoning: false,
        pages: '~10-15 stron PDF na raz',
        description: 'Poprzednia generacja modelu'
    },
    'gpt-4-turbo': {
        name: 'gpt-4-turbo',
        context: '128,000 tokenów',
        output: '4,096 tokenów',
        reasoning: false,
        pages: '~10-15 stron PDF na raz',
        description: 'Szybsza wersja GPT-4'
    },
    'gpt-4': {
        name: 'gpt-4',
        context: '8,192 tokenów',
        output: '4,096 tokenów',
        reasoning: false,
        pages: '~5-8 stron PDF na raz',
        description: 'Standardowy model GPT-4'
    }
};

function loadModelConfiguration() {
    // Load from localStorage
    const savedModel = localStorage.getItem('aiModel');
    const savedCustomModel = localStorage.getItem('customAiModel');

    if (savedModel) {
        currentAIModel = savedModel;
        document.getElementById('aiModelSelect').value = savedModel;

        if (savedModel === 'custom' && savedCustomModel) {
            document.getElementById('customModel').value = savedCustomModel;
            document.getElementById('customModelDiv').style.display = 'block';
        }
    }

    updateModelInfo();
}

function onModelSelectChange() {
    const selectElement = document.getElementById('aiModelSelect');
    const customModelDiv = document.getElementById('customModelDiv');
    const selectedValue = selectElement.value;

    if (selectedValue === 'custom') {
        customModelDiv.style.display = 'block';
    } else {
        customModelDiv.style.display = 'none';
    }

    updateModelInfo();
}

function updateModelInfo() {
    const selectElement = document.getElementById('aiModelSelect');
    const selectedValue = selectElement.value;
    const modelInfoDiv = document.getElementById('modelInfo');

    if (selectedValue === 'custom') {
        modelInfoDiv.innerHTML = `
            <p><strong>Własny model:</strong></p>
            <ul style="margin-left: 20px; margin-top: 5px;">
                <li>Wprowadź nazwę modelu zgodną z OpenAI API</li>
                <li>Upewnij się, że masz dostęp do tego modelu</li>
                <li>Parametry zależą od wybranego modelu</li>
            </ul>
        `;
    } else {
        const info = MODEL_INFO[selectedValue];
        if (info) {
            modelInfoDiv.innerHTML = `
                <p><strong>${info.name}:</strong></p>
                <ul style="margin-left: 20px; margin-top: 5px;">
                    <li>Context window: ${info.context}</li>
                    <li>Max output: ${info.output}</li>
                    ${info.reasoning ? '<li>Wsparcie reasoning tokens</li>' : ''}
                    <li>Zalecane ${info.pages}</li>
                    <li>${info.description}</li>
                </ul>
            `;
        }
    }
}

async function saveModelConfiguration() {
    const selectElement = document.getElementById('aiModelSelect');
    const selectedValue = selectElement.value;
    const statusSpan = document.getElementById('modelStatus');

    let modelToSave = selectedValue;

    if (selectedValue === 'custom') {
        const customModel = document.getElementById('customModel').value.trim();

        if (!customModel) {
            statusSpan.style.color = '#ef4444';
            statusSpan.textContent = 'Wprowadź nazwę własnego modelu!';
            setTimeout(() => { statusSpan.textContent = ''; }, 3000);
            return;
        }

        localStorage.setItem('customAiModel', customModel);
        currentAIModel = customModel;
    } else {
        currentAIModel = selectedValue;
        localStorage.removeItem('customAiModel');
    }

    // Save to localStorage
    localStorage.setItem('aiModel', selectedValue);

    // W produkcji: API call do backendu aby zapisać w konfiguracji/bazie
    // await fetch('/api/settings/model', {
    //     method: 'POST',
    //     headers: { 'Content-Type': 'application/json' },
    //     body: JSON.stringify({ model: currentAIModel })
    // });

    statusSpan.style.color = '#22c55e';
    statusSpan.textContent = `✓ Zapisano: ${currentAIModel}`;

    setTimeout(() => {
        statusSpan.textContent = '';
    }, 3000);
}

// ============== FILE MANAGEMENT ==============

async function loadPdfFiles() {
    const fileListDiv = document.getElementById('fileList');
    fileListDiv.innerHTML = '<p class="loading">Ładowanie listy plików...</p>';

    // W produkcji: API call do backendu
    // const response = await fetch('/api/files/list');
    // availableFiles = await response.json();

    // Mock data - te same pliki które widziałeś w folderze
    availableFiles = [
        'Fit_Ciasta_-_wersja_do_wydruku_6912109c7c823_e.pdf',
        'Fit_Desery_i_Koktajle_-_wersja_do_wydruku_6912109dbbf2b_e.pdf',
        'Fit_Fast_Food_-_wersja_do_wydruku_69121076abeb9_e.pdf',
        'Fit_Obiady_-_wersja_do_wydruku_69121077a8f92_e.pdf',
        'Fit_Słodycze_-_wersja_do_wydruku_6912109e9b854_e.pdf',
        'Fit_Słodycze_691210a2d0719_e.pdf',
        'Fit_Słodycze_w_10_Minut_-_wersja_do_wydruku_691210a0a0170_e.pdf',
        'Fit_Słodycze_z_Czterech_Składników_-_wersja_do_wydruku_691210a585dd9_e.pdf',
        'Fit-Dania-w-5-Minut-wersja-do-druku-1_6912107ebc5ed_e.pdf',
        'Fit-Dania-z-Restauracji-bkrfac_69121080a1362_e.pdf',
        'Fit-Ddania-w-15-Minut-wersja-do-wydruku-1_6912107d9fa0a_e.pdf',
        'Fit-Posilki-dla-Zabieganych-ml8joz_6912108317c0c_e.pdf',
        'Fit-Śniadania_6912107bc77c1_e.pdf',
        'Fit-Śniadania-wersja-do-druku_6912107a61e46_e.pdf',
        'Jadlospis-Domowy-Fast-Food-imcweq_69121087d50b0_e.pdf',
        'Jadlospis-Dzien-jedzenia-w-15-minut-4ryhjr_69121088a24a8_e.pdf',
        'Jadlospis-Dzien-jedzenia-w-25-minut-ijuwpl_691210893b029_e.pdf',
        'Jadlospis-Legalne-slodycze-rsgnas_691210aeb2ee1_e.pdf',
        'Jadlospis-Legalne-slodycze-rsgnas_691210b349d34_e.pdf',
        'Jadlospis-Slodka-redukcja-bhl6g2_691210ad974f3_e.pdf',
        'Jadlospis-Weekendowy-dla-par-lhd4cv_691210ae3a18b_e.pdf',
        'Jadlospis-Weekendowy-dla-par-nfocdr_69121095d503b_e.pdf',
        'Jadlospis-Ze-sniadaniem-na-slodko-d5cwk3_691210afa7a5d_e.pdf',
        'smakolyki_druk-cmaaig_691210a6d7bc4_e.pdf'
    ];

    renderFileList();
}

function renderFileList() {
    const fileListDiv = document.getElementById('fileList');

    if (availableFiles.length === 0) {
        fileListDiv.innerHTML = '<p class="loading">Brak plików w folderze</p>';
        return;
    }

    let html = '';
    availableFiles.forEach((file, index) => {
        const isSelected = selectedFiles.includes(file);
        html += `
            <div class="file-item">
                <input type="checkbox"
                       id="file-${index}"
                       ${isSelected ? 'checked' : ''}
                       onchange="toggleFileSelection('${file}')">
                <label for="file-${index}">${file}</label>
            </div>
        `;
    });

    fileListDiv.innerHTML = html;
}

function toggleFileSelection(filename) {
    const index = selectedFiles.indexOf(filename);
    if (index > -1) {
        selectedFiles.splice(index, 1);
    } else {
        selectedFiles.push(filename);
    }
}

function selectAllFiles() {
    selectedFiles = [...availableFiles];
    renderFileList();
}

function deselectAllFiles() {
    selectedFiles = [];
    renderFileList();
}

async function processSelectedFiles() {
    if (selectedFiles.length === 0) {
        alert('Nie zaznaczono żadnych plików!');
        return;
    }

    if (!confirm(`Czy na pewno chcesz przetworzyć ${selectedFiles.length} plików?`)) {
        return;
    }

    // W produkcji: API call do backendu .NET który uruchomi przetwarzanie
    // await fetch('/api/recipes/process', {
    //     method: 'POST',
    //     headers: { 'Content-Type': 'application/json' },
    //     body: JSON.stringify({ files: selectedFiles })
    // });

    alert(`Rozpoczęto przetwarzanie ${selectedFiles.length} plików!\n\nPrzejdź do aplikacji konsolowej aby zobaczyć postęp.`);
}

// ============== DATABASE MANAGEMENT ==============

async function loadRecipesFromDatabase() {
    // W produkcji: API call do backendu
    // const response = await fetch('/api/recipes');
    // recipesDatabase = await response.json();

    // Mock data dla testów
    const storedRecipes = localStorage.getItem('recipes');
    if (storedRecipes) {
        recipesDatabase = JSON.parse(storedRecipes);
    } else {
        recipesDatabase = [
            {
                id: 1,
                name: "Owsianka z owocami",
                description: "Zdrowe i pożywne śniadanie",
                ingredients: "Płatki owsiane\nMleko\nBanany\nJagody",
                instructions: "1. Zagotuj mleko\n2. Dodaj płatki\n3. Gotuj 5 minut\n4. Dodaj owoce",
                calories: 350,
                protein: 12,
                carbohydrates: 55,
                fat: 8,
                mealType: "Sniadanie"
            },
            {
                id: 2,
                name: "Kurczak z ryżem i warzywami",
                description: "Zbilansowany obiad",
                ingredients: "Pierś z kurczaka\nRyż brązowy\nBrokuły\nMarchewka",
                instructions: "1. Ugotuj ryż\n2. Usmaż kurczaka\n3. Ugotuj warzywa na parze\n4. Podawaj razem",
                calories: 520,
                protein: 45,
                carbohydrates: 60,
                fat: 12,
                mealType: "Obiad"
            },
            {
                id: 3,
                name: "Łosoś z batatami",
                description: "Kolacja bogata w omega-3",
                ingredients: "Filet z łososia\nBataty\nSzpinak",
                instructions: "1. Upiecz bataty\n2. Usmaż łososia\n3. Przygotuj szpinak\n4. Podawaj na ciepło",
                calories: 480,
                protein: 38,
                carbohydrates: 45,
                fat: 18,
                mealType: "Kolacja"
            }
        ];
        saveRecipesToLocalStorage();
    }
}

function loadDatabase() {
    const viewerDiv = document.getElementById('databaseViewer');
    viewerDiv.innerHTML = '<p class="loading">Ładowanie przepisów z bazy...</p>';

    setTimeout(() => {
        renderDatabaseTable(recipesDatabase);
    }, 500);
}

function refreshDatabase() {
    loadDatabase();
}

function renderDatabaseTable(recipes) {
    const viewerDiv = document.getElementById('databaseViewer');

    if (recipes.length === 0) {
        viewerDiv.innerHTML = '<p class="loading">Baza danych jest pusta. Przetwórz pliki PDF aby dodać przepisy.</p>';
        return;
    }

    let html = `
        <div style="overflow-x: auto;">
            <table class="recipe-table">
                <thead>
                    <tr>
                        <th>ID</th>
                        <th>Nazwa</th>
                        <th>Kategoria</th>
                        <th>Kalorie</th>
                        <th>Białko</th>
                        <th>Węgl.</th>
                        <th>Tłuszcze</th>
                        <th>Akcje</th>
                    </tr>
                </thead>
                <tbody>
    `;

    recipes.forEach(recipe => {
        html += `
            <tr>
                <td>${recipe.id}</td>
                <td>${recipe.name}</td>
                <td>${recipe.mealType}</td>
                <td>${recipe.calories}</td>
                <td>${recipe.protein}g</td>
                <td>${recipe.carbohydrates}g</td>
                <td>${recipe.fat}g</td>
                <td>
                    <div class="recipe-actions">
                        <button class="btn btn-warning" onclick="editRecipe(${recipe.id})">Edytuj</button>
                        <button class="btn btn-danger" onclick="deleteRecipe(${recipe.id})">Usuń</button>
                    </div>
                </td>
            </tr>
        `;
    });

    html += `
                </tbody>
            </table>
        </div>
        <p style="margin-top: 20px; color: #9ca3af;">Łącznie przepisów: ${recipes.length}</p>
    `;

    viewerDiv.innerHTML = html;
}

function searchDatabase() {
    const searchTerm = document.getElementById('searchDb').value.toLowerCase();

    if (!searchTerm) {
        renderDatabaseTable(recipesDatabase);
        return;
    }

    const filtered = recipesDatabase.filter(recipe =>
        recipe.name.toLowerCase().includes(searchTerm) ||
        recipe.description.toLowerCase().includes(searchTerm) ||
        recipe.mealType.toLowerCase().includes(searchTerm)
    );

    renderDatabaseTable(filtered);
}

function editRecipe(id) {
    const recipe = recipesDatabase.find(r => r.id === id);
    if (!recipe) return;

    document.getElementById('editRecipeId').value = recipe.id;
    document.getElementById('editName').value = recipe.name;
    document.getElementById('editDescription').value = recipe.description;
    document.getElementById('editCategory').value = recipe.mealType;
    document.getElementById('editIngredients').value = recipe.ingredients.replace(/\n/g, '\n');
    document.getElementById('editInstructions').value = recipe.instructions;
    document.getElementById('editCalories').value = recipe.calories;
    document.getElementById('editProtein').value = recipe.protein;
    document.getElementById('editCarbs').value = recipe.carbohydrates;
    document.getElementById('editFat').value = recipe.fat;

    document.getElementById('editModal').style.display = 'block';
}

function closeEditModal() {
    document.getElementById('editModal').style.display = 'none';
}

async function saveRecipeEdit(e) {
    e.preventDefault();

    const id = parseInt(document.getElementById('editRecipeId').value);
    const recipe = recipesDatabase.find(r => r.id === id);

    if (!recipe) return;

    recipe.name = document.getElementById('editName').value;
    recipe.description = document.getElementById('editDescription').value;
    recipe.mealType = document.getElementById('editCategory').value;
    recipe.ingredients = document.getElementById('editIngredients').value;
    recipe.instructions = document.getElementById('editInstructions').value;
    recipe.calories = parseInt(document.getElementById('editCalories').value);
    recipe.protein = parseFloat(document.getElementById('editProtein').value);
    recipe.carbohydrates = parseFloat(document.getElementById('editCarbs').value);
    recipe.fat = parseFloat(document.getElementById('editFat').value);

    // W produkcji: API call
    // await fetch(`/api/recipes/${id}`, {
    //     method: 'PUT',
    //     headers: { 'Content-Type': 'application/json' },
    //     body: JSON.stringify(recipe)
    // });

    saveRecipesToLocalStorage();
    closeEditModal();
    loadDatabase();
    alert('Przepis został zaktualizowany!');
}

async function deleteRecipe(id) {
    const recipe = recipesDatabase.find(r => r.id === id);
    if (!recipe) return;

    if (!confirm(`Czy na pewno chcesz usunąć przepis "${recipe.name}"?`)) {
        return;
    }

    // W produkcji: API call
    // await fetch(`/api/recipes/${id}`, { method: 'DELETE' });

    const index = recipesDatabase.findIndex(r => r.id === id);
    recipesDatabase.splice(index, 1);

    saveRecipesToLocalStorage();
    loadDatabase();
    alert('Przepis został usunięty!');
}

function saveRecipesToLocalStorage() {
    localStorage.setItem('recipes', JSON.stringify(recipesDatabase));
}

// ============== MEAL PLANNING ==============

function getRandomRecipeByType(mealType) {
    const recipes = recipesDatabase.filter(r => r.mealType === mealType);
    if (recipes.length === 0) return null;
    return recipes[Math.floor(Math.random() * recipes.length)];
}

function generateDailyPlan() {
    const breakfast = getRandomRecipeByType('Sniadanie');
    const lunch = getRandomRecipeByType('Obiad');
    const dinner = getRandomRecipeByType('Kolacja');

    const dailyPlanDiv = document.getElementById('dailyPlan');

    if (!breakfast || !lunch || !dinner) {
        dailyPlanDiv.innerHTML = '<p class="loading">Za mało przepisów w bazie! Przetwórz najpierw pliki PDF.</p>';
        return;
    }

    const totalCalories = breakfast.calories + lunch.calories + dinner.calories;
    const totalProtein = breakfast.protein + lunch.protein + dinner.protein;
    const totalCarbs = breakfast.carbohydrates + lunch.carbohydrates + dinner.carbohydrates;
    const totalFat = breakfast.fat + lunch.fat + dinner.fat;

    dailyPlanDiv.innerHTML = `
        <div class="day-plan">
            <h3>Plan na Dziś</h3>

            <div class="meal">
                <h4>Śniadanie</h4>
                <div class="meal-name">${breakfast.name}</div>
                <p style="color: #9ca3af; margin-top: 5px;">${breakfast.description}</p>
                <div class="meal-nutrition">
                    <span class="nutrition-item">Kalorie: ${breakfast.calories} kcal</span>
                    <span class="nutrition-item">Białko: ${breakfast.protein}g</span>
                    <span class="nutrition-item">Węglowodany: ${breakfast.carbohydrates}g</span>
                    <span class="nutrition-item">Tłuszcze: ${breakfast.fat}g</span>
                </div>
            </div>

            <div class="meal">
                <h4>Obiad</h4>
                <div class="meal-name">${lunch.name}</div>
                <p style="color: #9ca3af; margin-top: 5px;">${lunch.description}</p>
                <div class="meal-nutrition">
                    <span class="nutrition-item">Kalorie: ${lunch.calories} kcal</span>
                    <span class="nutrition-item">Białko: ${lunch.protein}g</span>
                    <span class="nutrition-item">Węglowodany: ${lunch.carbohydrates}g</span>
                    <span class="nutrition-item">Tłuszcze: ${lunch.fat}g</span>
                </div>
            </div>

            <div class="meal">
                <h4>Kolacja</h4>
                <div class="meal-name">${dinner.name}</div>
                <p style="color: #9ca3af; margin-top: 5px;">${dinner.description}</p>
                <div class="meal-nutrition">
                    <span class="nutrition-item">Kalorie: ${dinner.calories} kcal</span>
                    <span class="nutrition-item">Białko: ${dinner.protein}g</span>
                    <span class="nutrition-item">Węglowodany: ${dinner.carbohydrates}g</span>
                    <span class="nutrition-item">Tłuszcze: ${dinner.fat}g</span>
                </div>
            </div>

            <div class="daily-totals">
                <h3>Dzienne Podsumowanie</h3>
                <div class="totals-grid">
                    <div class="total-item">
                        <span class="value">${totalCalories}</span>
                        <span class="label">Kalorie</span>
                    </div>
                    <div class="total-item">
                        <span class="value">${totalProtein.toFixed(1)}g</span>
                        <span class="label">Białko</span>
                    </div>
                    <div class="total-item">
                        <span class="value">${totalCarbs.toFixed(1)}g</span>
                        <span class="label">Węglowodany</span>
                    </div>
                    <div class="total-item">
                        <span class="value">${totalFat.toFixed(1)}g</span>
                        <span class="label">Tłuszcze</span>
                    </div>
                </div>
            </div>
        </div>
    `;
}

function generateWeeklyPlan() {
    const startDate = new Date(document.getElementById('startDate').value);
    const weeklyPlanDiv = document.getElementById('weeklyPlan');

    currentWeeklyPlan = {};
    let html = '';

    for (let i = 0; i < 7; i++) {
        const currentDate = new Date(startDate);
        currentDate.setDate(startDate.getDate() + i);

        const dateStr = currentDate.toLocaleDateString('pl-PL', {
            weekday: 'long',
            year: 'numeric',
            month: 'long',
            day: 'numeric'
        });

        const breakfast = getRandomRecipeByType('Sniadanie');
        const lunch = getRandomRecipeByType('Obiad');
        const dinner = getRandomRecipeByType('Kolacja');

        if (!breakfast || !lunch || !dinner) {
            weeklyPlanDiv.innerHTML = '<p class="loading">Za mało przepisów w bazie! Przetwórz najpierw pliki PDF.</p>';
            return;
        }

        currentWeeklyPlan[dateStr] = { breakfast, lunch, dinner };

        const totalCalories = breakfast.calories + lunch.calories + dinner.calories;

        html += `
            <div class="day-plan">
                <h3>${dateStr}</h3>

                <div class="meal">
                    <h4>Śniadanie</h4>
                    <div class="meal-name">${breakfast.name}</div>
                    <div class="meal-nutrition">
                        <span class="nutrition-item">${breakfast.calories} kcal</span>
                        <span class="nutrition-item">B: ${breakfast.protein}g</span>
                        <span class="nutrition-item">W: ${breakfast.carbohydrates}g</span>
                        <span class="nutrition-item">T: ${breakfast.fat}g</span>
                    </div>
                </div>

                <div class="meal">
                    <h4>Obiad</h4>
                    <div class="meal-name">${lunch.name}</div>
                    <div class="meal-nutrition">
                        <span class="nutrition-item">${lunch.calories} kcal</span>
                        <span class="nutrition-item">B: ${lunch.protein}g</span>
                        <span class="nutrition-item">W: ${lunch.carbohydrates}g</span>
                        <span class="nutrition-item">T: ${lunch.fat}g</span>
                    </div>
                </div>

                <div class="meal">
                    <h4>Kolacja</h4>
                    <div class="meal-name">${dinner.name}</div>
                    <div class="meal-nutrition">
                        <span class="nutrition-item">${dinner.calories} kcal</span>
                        <span class="nutrition-item">B: ${dinner.protein}g</span>
                        <span class="nutrition-item">W: ${dinner.carbohydrates}g</span>
                        <span class="nutrition-item">T: ${dinner.fat}g</span>
                    </div>
                </div>

                <div style="margin-top: 10px; padding: 12px; background: #1f1f1f; border-radius: 6px; border: 1px solid #333;">
                    <strong style="color: #ffffff;">Suma dzienna: ${totalCalories} kcal</strong>
                </div>
            </div>
        `;
    }

    weeklyPlanDiv.innerHTML = html;
}

function printWeeklyPlan() {
    const printArea = document.getElementById('printArea');
    let html = '<div class="print-header"><h1>Tygodniowy Plan Posiłków</h1></div>';

    for (const [date, meals] of Object.entries(currentWeeklyPlan)) {
        html += `
            <div class="print-day">
                <h2>${date}</h2>
                <div style="margin-left: 20px;">
                    <h3>Śniadanie</h3>
                    <p><strong>${meals.breakfast.name}</strong></p>
                    <p>Składniki: ${meals.breakfast.ingredients.replace(/\n/g, ', ')}</p>
                    <p>Kalorie: ${meals.breakfast.calories} kcal | Białko: ${meals.breakfast.protein}g | Węgl: ${meals.breakfast.carbohydrates}g | Tłuszcze: ${meals.breakfast.fat}g</p>

                    <h3>Obiad</h3>
                    <p><strong>${meals.lunch.name}</strong></p>
                    <p>Składniki: ${meals.lunch.ingredients.replace(/\n/g, ', ')}</p>
                    <p>Kalorie: ${meals.lunch.calories} kcal | Białko: ${meals.lunch.protein}g | Węgl: ${meals.lunch.carbohydrates}g | Tłuszcze: ${meals.lunch.fat}g</p>

                    <h3>Kolacja</h3>
                    <p><strong>${meals.dinner.name}</strong></p>
                    <p>Składniki: ${meals.dinner.ingredients.replace(/\n/g, ', ')}</p>
                    <p>Kalorie: ${meals.dinner.calories} kcal | Białko: ${meals.dinner.protein}g | Węgl: ${meals.dinner.carbohydrates}g | Tłuszcze: ${meals.dinner.fat}g</p>
                </div>
            </div>
        `;
    }

    printArea.innerHTML = html;
    window.print();
}

// ============== SHOPPING LIST ==============

function generateShoppingList() {
    const shoppingListDiv = document.getElementById('shoppingList');

    if (Object.keys(currentWeeklyPlan).length === 0) {
        shoppingListDiv.innerHTML = '<p class="loading">Najpierw wygeneruj tygodniowy plan posiłków!</p>';
        return;
    }

    const ingredients = new Map();

    for (const [date, meals] of Object.entries(currentWeeklyPlan)) {
        for (const meal of Object.values(meals)) {
            const items = meal.ingredients.split('\n').filter(i => i.trim());
            items.forEach(item => {
                if (item.trim()) {
                    const count = ingredients.get(item) || 0;
                    ingredients.set(item, count + 1);
                }
            });
        }
    }

    let html = '<div class="shopping-category"><h3>Lista Zakupów na Tydzień</h3><ul>';

    for (const [ingredient, count] of ingredients.entries()) {
        html += `<li>${ingredient} ${count > 1 ? `(x${count})` : ''}</li>`;
    }

    html += '</ul></div>';
    shoppingListDiv.innerHTML = html;
}

async function exportToTodoist() {
    const todoistApiKey = prompt('Wprowadź swój klucz API Todoist:');

    if (!todoistApiKey) {
        alert('Brak klucza API');
        return;
    }

    const shoppingListDiv = document.getElementById('shoppingList');
    const items = shoppingListDiv.querySelectorAll('li');

    if (items.length === 0) {
        alert('Najpierw wygeneruj listę zakupów!');
        return;
    }

    try {
        for (const item of items) {
            const taskContent = item.textContent;

            const response = await fetch(TODOIST_API_URL, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${todoistApiKey}`,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    content: taskContent,
                    project_id: null
                })
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
        }

        alert('Lista zakupów została wyeksportowana do Todoist!');
    } catch (error) {
        console.error('Błąd podczas eksportu do Todoist:', error);
        alert('Błąd podczas eksportu do Todoist. Sprawdź konsolę dla szczegółów.');
    }
}

// Close modal when clicking outside
window.onclick = function(event) {
    const modal = document.getElementById('editModal');
    if (event.target == modal) {
        closeEditModal();
    }
}
