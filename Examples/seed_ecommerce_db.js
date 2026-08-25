// 1. Clean up existing data (Optional: Comment out if you want to append)
db.users.findall().delete();
db.products.findall().delete();
db.orders.findall().delete();
db.categories.findall().delete();
db.reviews.findall().delete();
db.audit_logs.findall().delete();

// 2. Configuration for Scale
const SCALE = {
    USERS: 100,      // 100 users
    CATEGORIES: 5,   // 5 categories
    PRODUCTS: 500,   // 500 products
    ORDERS: 1000,    // 1000 orders
    REVIEWS: 1000    // 1000 reviews
};

// 3. Helper Functions (Utilizing new UUID v7 functions and standard JavaScript math)
function getRandomInt(min, max) {
    return Math.floor(Math.random() * (max - min + 1)) + min;
}

function getRandomItem(array) {
    if (!array || array.length === 0) return null;
    return array[Math.floor(Math.random() * array.length)];
}

function getRandomDate(start, end) {
    return new Date(start.getTime() + Math.random() * (end.getTime() - start.getTime()));
}

// 4. OpenAI Integration Example (Mock Setup)
db.saveView("generateProductDesc", `
// @access private
var productName = @name;
var token = $OPENAI_KEY; // Assumes OPENAI_KEY is in Vault
if (!token) return "No OpenAI Token configured";

var llm = openai("https://api.openai.com/v1", token);
llm.addSysMsg("You are a creative copywriter. Write a 1-sentence product description.");
var desc = llm.msg("Write a description for: " + productName, {}, "gpt-3.5-turbo");
return desc;
`);

// 5. Queue Integration Example (using new UUID v7 generation for task queuing)
function queueOrderProcessing(orderId) {
    var script = `
        var order = db.orders.find(o => o._id == @id);
        if (order) {
            db.orders.update(o => o._id == @id, { 
                processedAt: new Date(), 
                status: 'Processed' 
            });
            console.log("Background processed order: " + @id);
        }
    `;
    // Queue with priority 1
    queue(script, { id: orderId }, { priority: 1 });
}

// 6. User Data Generation
console.log("Generating " + SCALE.USERS + " Users...");
var firstNames = ["James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", "William", "Elizabeth", "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Charles", "Karen"];
var lastNames = ["Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin"];
var countries = ["USA", "UK", "Germany", "France", "Canada", "Australia", "Japan", "China", "Brazil", "India"];
var domains = ["gmail.com", "yahoo.com", "hotmail.com", "outlook.com", "icloud.com"];

for (var i = 0; i < SCALE.USERS; i++) {
    var firstName = getRandomItem(firstNames);
    var lastName = getRandomItem(lastNames);
    var email = firstName.toLowerCase() + "." + lastName.toLowerCase() + i + "@" + getRandomItem(domains);
    var randomRegDate = getRandomDate(new Date(2023, 0, 1), new Date());

    db.users.insert({
        _id: guidv7(randomRegDate.toISOString()), // Use new guidv7(datetime) for time-ordering
        firstName: firstName,
        lastName: lastName,
        email: email,
        password: sha256("password" + i),
        country: getRandomItem(countries),
        age: getRandomInt(18, 70),
        isActive: Math.random() > 0.1,
        createdAt: randomRegDate,
        isPremium: Math.random() > 0.8
    });
}
var userList = db.users.findall().toList();
if (userList.length === 0) throw new Error("User generation failed. userList is empty.");
console.log("Users loaded: " + userList.length);

// 7. Category Data Generation
console.log("Generating " + SCALE.CATEGORIES + " Categories...");
var catNames = ["Electronics", "Fashion", "Home", "Books", "Sports", "Toys", "Health", "Automotive", "Beauty", "Groceries", "Music", "Movies", "Games", "Tools", "Outdoors", "Computers", "Pet", "Kids", "Industrial", "Handmade"];
for (var i = 0; i < SCALE.CATEGORIES; i++) {
    var base = getRandomItem(catNames);
    db.categories.insert({
        name: base + " " + (i + 1),
        description: "Category for " + base,
        taxRate: getRandomInt(0, 20) / 100
    });
}
var categoryList = db.categories.findall().toList();
if (categoryList.length === 0) throw new Error("Category generation failed. categoryList is empty.");
console.log("Categories loaded: " + categoryList.length);

// 8. Product Data Generation
console.log("Generating " + SCALE.PRODUCTS + " Products...");
var productPrefixes = ["Super", "Ultra", "Mega", "Pro", "Max", "Eco", "Smart", "Compact", "Luxury", "Budget"];
var productNouns = ["Phone", "Laptop", "Shirt", "Shoes", "Table", "Chair", "Book", "Ball", "Doll", "Vitamin", "Tire", "Lipstick", "Coffee", "Watch", "Headphones", "Camera", "Monitor", "Keyboard", "Mouse", "Speaker"];

for (var i = 0; i < SCALE.PRODUCTS; i++) {
    var category = getRandomItem(categoryList);
    var name = getRandomItem(productPrefixes) + " " + getRandomItem(productNouns) + " " + i;
    var randomProdDate = getRandomDate(new Date(2023, 0, 1), new Date());

    db.products.insert({
        _id: guidv7(randomProdDate.toISOString()), // Order products chronologically via v7 UUID
        name: name,
        description: "High quality " + name + " for your needs.",
        price: Math.round((Math.random() * 990 + 10) * 100) / 100,
        categoryId: category._id,
        categoryName: category.name,
        stock: getRandomInt(0, 500),
        rating: Math.round((Math.random() * 4 + 1) * 10) / 10,
        tags: [category.name.toLowerCase().split(' ')[0], "sale", "new"],
        createdAt: randomProdDate
    });
}
var productList = db.products.findall().toList();
if (productList.length === 0) throw new Error("Product generation failed. productList is empty.");
console.log("Products loaded: " + productList.length);

// 9. Order Data Generation
console.log("Generating " + SCALE.ORDERS + " Orders...");
var orderStatuses = ["Pending", "Processing", "Shipped", "Delivered", "Cancelled"];

for (var i = 0; i < SCALE.ORDERS; i++) {
    var user = getRandomItem(userList);
    if (!user) { console.log("Warning: null user at order " + i); continue; }

    var itemCount = getRandomInt(1, 4);
    var items = [];
    var totalAmount = 0;

    for (var j = 0; j < itemCount; j++) {
        var product = getRandomItem(productList);
        if (!product) { console.log("Warning: null product at order " + i + " item " + j); continue; }
        var quantity = getRandomInt(1, 3);

        items.push({
            productId: product._id,
            productName: product.name, // Denormalized name
            quantity: quantity,
            price: product.price
        });
        totalAmount += product.price * quantity;
    }

    var randomOrderDate = getRandomDate(new Date(2023, 0, 1), new Date());
    var orderId = db.orders.insert({
        _id: guidv7(randomOrderDate.toISOString()), // Store order date metadata directly in primary key
        userId: user._id,
        userEmail: user.email,
        items: items,
        totalAmount: Math.round(totalAmount * 100) / 100,
        status: getRandomItem(orderStatuses),
        shippingAddress: {
            street: getRandomInt(100, 999) + " Main St",
            city: "City " + getRandomInt(1, 100),
            country: user.country,
            zipCode: getRandomInt(10000, 99999).toString()
        },
        paymentMethod: getRandomItem(["Credit Card", "PayPal", "Bank Transfer"]),
        createdAt: randomOrderDate
    })._id;

    // Process 1% of orders via background queue
    if (i % 100 === 0) {
        queueOrderProcessing(orderId);
    }
}

// 10. Reviews Generation
console.log("Generating " + SCALE.REVIEWS + " Reviews...");
var reviewTexts = ["Great!", "Bad.", "Okay.", "Love it.", "Hate it."];

for (var i = 0; i < SCALE.REVIEWS; i++) {
    var user = getRandomItem(userList);
    var product = getRandomItem(productList);

    if (!user || !product) {
        continue;
    }

    var randomReviewDate = getRandomDate(new Date(2023, 0, 1), new Date());
    db.reviews.insert({
        _id: guidv7(randomReviewDate.toISOString()), // Ordered by review timestamp
        userId: user._id,
        productId: product._id,
        rating: getRandomInt(1, 5),
        comment: getRandomItem(reviewTexts),
        createdAt: randomReviewDate
    });
}

// 11. Views & Scripts
db.saveView("searchProducts", `
// @access public
var keyword = @keyword;
if (!keyword) return [];
// Substring search with AxarDB case-insensitive String.prototype.contains
return db.products.findall(p => p.name.contains(keyword)).toList();
`);

// 12. View Using OpenAI (Demonstration)
db.saveView("askAI", `
// @access public
var question = @question;
var token = $OPENAI_KEY;
if (!token) return { error: "No API Key" };
var llm = openai("https://api.openai.com/v1", token);
return llm.msg(question, {}, "gpt-3.5-turbo");
`);

// 13. Bulk Collections Example (JSONL Storage for Lookup Tables)
console.log("Seeding Static Bulk Lookup Tables...");
bulk.taxRates.insert([
    { country: "TR", rate: 0.20, category: "Standard" },
    { country: "DE", rate: 0.19, category: "Standard" },
    { country: "US", rate: 0.08, category: "StateTax" }
]);
console.log("Bulk Collection (taxRates) Count: " + bulk.taxRates.findall().count());

// 14. Memory Collections Example (TTL In-Memory Temporary Storage)
console.log("Seeding Temporary Memory Cache & Sessions...");
memory.activeCarts.insert({ cartId: "cart_999", userId: userList[0]._id, items: ["prod_1", "prod_2"] }, 0.5); // 30 mins TTL
memory.activeCarts.insert({ cartId: "cart_888", userId: userList[1]._id, items: ["prod_3"] }); // default 1 hour TTL
console.log("Active Memory Carts Count: " + memory.activeCarts.findall().count());

// 15. Pagination & Date Query Verification (Using the new guidv7CreatedAt utility)
console.log("Testing Pagination with skip(n)...");
var firstFive = db.products.findall().take(5).toList();
var pageTwo = db.products.findall().skip(5).take(5).toList();
console.log("Page 1 (first 5) sample: " + (firstFive[0] ? firstFive[0].name : "None"));

if (firstFive[0]) {
    var extractedTime = guidv7CreatedAt(firstFive[0]._id);
    console.log("Extracted Product Creation Time from UUID v7: " + extractedTime);
}

console.log("Seeding Complete!");
return {
    success: true,
    stats: {
        users: db.users.findall().count(),
        products: db.products.findall().count(),
        orders: db.orders.findall().count(),
        reviews: db.reviews.findall().count()
    },
    message: "Database hydrated with time-ordered UUID v7 records."
};
