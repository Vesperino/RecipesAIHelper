// Mock data - in production, this would connect to your .NET backend via API
let recipesDatabase = [];
let currentWeeklyPlan = {};

// Configuration
const TODOIST_API_URL = 'https://api.todoist.com/rest/v2/tasks';
const TODOIST_PROJECT_NAME = 'Shopping List';

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    // Set default date to today
    document.getElementById('startDate').valueAsDate = new Date();

    // Event listeners
    document.getElementById('generateDailyBtn').addEventListener('click', generateDailyPlan);
    document.getElementById('generateWeeklyBtn').addEventListener('click', generateWeeklyPlan);
    document.getElementById('printWeeklyBtn').addEventListener('click', printWeeklyPlan);
    document.getElementById('generateShoppingListBtn').addEventListener('click', generateShoppingList);
    document.getElementById('exportTodoistBtn').addEventListener('click', exportToTodoist);
    document.getElementById('loadRecipesBtn').addEventListener('click', loadAllRecipes);

    // Load recipes from SQLite (you'll need to implement an API endpoint)
    loadRecipesFromDatabase();
});

async function loadRecipesFromDatabase() {
    // In production, this would fetch from your .NET backend
    // For now, we'll use localStorage as a mock
    const storedRecipes = localStorage.getItem('recipes');
    if (storedRecipes) {
        recipesDatabase = JSON.parse(storedRecipes);
    } else {
        // Sample data for testing
        recipesDatabase = [
            {
                id: 1,
                name: "Owsianka z owocami",
                description: "Zdrowe i pożywne śniadanie",
                ingredients: "Płatki owsiane, mleko, banany, jagody",
                calories: 350,
                protein: 12,
                carbohydrates: 55,
                fat: 8,
                mealType: "Breakfast"
            },
            {
                id: 2,
                name: "Kurczak z ryżem i warzywami",
                description: "Zbilansowany obiad",
                ingredients: "Pierś z kurczaka, ryż brązowy, brokuły, marchewka",
                calories: 520,
                protein: 45,
                carbohydrates: 60,
                fat: 12,
                mealType: "Lunch"
            },
            {
                id: 3,
                name: "Łosoś z batatami",
                description: "Kolacja bogata w omega-3",
                ingredients: "Filet z łososia, bataty, szpinak",
                calories: 480,
                protein: 38,
                carbohydrates: 45,
                fat: 18,
                mealType: "Dinner"
            }
        ];
    }
}

function getRandomRecipeByType(mealType) {
    const recipes = recipesDatabase.filter(r => r.mealType === mealType);
    if (recipes.length === 0) return null;
    return recipes[Math.floor(Math.random() * recipes.length)];
}

function generateDailyPlan() {
    const breakfast = getRandomRecipeByType('Breakfast');
    const lunch = getRandomRecipeByType('Lunch');
    const dinner = getRandomRecipeByType('Dinner');

    const dailyPlanDiv = document.getElementById('dailyPlan');

    if (!breakfast || !lunch || !dinner) {
        dailyPlanDiv.innerHTML = '<p class="loading">Not enough recipes in database. Please process PDF files first!</p>';
        return;
    }

    const totalCalories = breakfast.calories + lunch.calories + dinner.calories;
    const totalProtein = breakfast.protein + lunch.protein + dinner.protein;
    const totalCarbs = breakfast.carbohydrates + lunch.carbohydrates + dinner.carbohydrates;
    const totalFat = breakfast.fat + lunch.fat + dinner.fat;

    dailyPlanDiv.innerHTML = `
        <div class="day-plan">
            <h3>Today's Meal Plan</h3>

            <div class="meal">
                <h4>Śniadanie</h4>
                <div class="meal-name">${breakfast.name}</div>
                <p>${breakfast.description}</p>
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
                <p>${lunch.description}</p>
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
                <p>${dinner.description}</p>
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

        const breakfast = getRandomRecipeByType('Breakfast');
        const lunch = getRandomRecipeByType('Lunch');
        const dinner = getRandomRecipeByType('Dinner');

        if (!breakfast || !lunch || !dinner) {
            weeklyPlanDiv.innerHTML = '<p class="loading">Not enough recipes in database. Please process PDF files first!</p>';
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

                <div style="margin-top: 10px; padding: 10px; background: #f0f0f0; border-radius: 5px;">
                    <strong>Suma dzienna: ${totalCalories} kcal</strong>
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
                    <p>Składniki: ${meals.breakfast.ingredients}</p>
                    <p>Kalorie: ${meals.breakfast.calories} kcal | Białko: ${meals.breakfast.protein}g | Węgl: ${meals.breakfast.carbohydrates}g | Tłuszcze: ${meals.breakfast.fat}g</p>

                    <h3>Obiad</h3>
                    <p><strong>${meals.lunch.name}</strong></p>
                    <p>Składniki: ${meals.lunch.ingredients}</p>
                    <p>Kalorie: ${meals.lunch.calories} kcal | Białko: ${meals.lunch.protein}g | Węgl: ${meals.lunch.carbohydrates}g | Tłuszcze: ${meals.lunch.fat}g</p>

                    <h3>Kolacja</h3>
                    <p><strong>${meals.dinner.name}</strong></p>
                    <p>Składniki: ${meals.dinner.ingredients}</p>
                    <p>Kalorie: ${meals.dinner.calories} kcal | Białko: ${meals.dinner.protein}g | Węgl: ${meals.dinner.carbohydrates}g | Tłuszcze: ${meals.dinner.fat}g</p>
                </div>
            </div>
        `;
    }

    printArea.innerHTML = html;
    window.print();
}

function generateShoppingList() {
    const shoppingListDiv = document.getElementById('shoppingList');

    if (Object.keys(currentWeeklyPlan).length === 0) {
        shoppingListDiv.innerHTML = '<p class="loading">Proszę najpierw wygenerować tygodniowy plan posiłków!</p>';
        return;
    }

    const ingredients = new Map();

    for (const [date, meals] of Object.entries(currentWeeklyPlan)) {
        for (const meal of Object.values(meals)) {
            const items = meal.ingredients.split(',').map(i => i.trim());
            items.forEach(item => {
                if (item) {
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
                    project_id: null // Use default Inbox
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

function loadAllRecipes() {
    const recipesListDiv = document.getElementById('recipesList');

    if (recipesDatabase.length === 0) {
        recipesListDiv.innerHTML = '<p class="loading">Brak przepisów w bazie danych. Przetworz najpierw pliki PDF!</p>';
        return;
    }

    let html = '';

    recipesDatabase.forEach(recipe => {
        html += `
            <div class="recipe-card">
                <span class="recipe-type">${recipe.mealType}</span>
                <h3>${recipe.name}</h3>
                <p>${recipe.description}</p>
                <div class="meal-nutrition">
                    <span class="nutrition-item">${recipe.calories} kcal</span>
                    <span class="nutrition-item">B: ${recipe.protein}g</span>
                    <span class="nutrition-item">W: ${recipe.carbohydrates}g</span>
                    <span class="nutrition-item">T: ${recipe.fat}g</span>
                </div>
                <p style="margin-top: 10px; font-size: 0.9em; color: #666;"><strong>Składniki:</strong> ${recipe.ingredients}</p>
            </div>
        `;
    });

    recipesListDiv.innerHTML = html;
}

// Helper function to save recipes to localStorage (for testing)
function saveRecipesToLocalStorage(recipes) {
    localStorage.setItem('recipes', JSON.stringify(recipes));
    recipesDatabase = recipes;
}
