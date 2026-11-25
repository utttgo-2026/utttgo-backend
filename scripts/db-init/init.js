/*
 * Idempotently initializes the MongoDB structures used by Catalog.
 * Run with: mongosh "$env:MONGODB_URI" scripts/db-init/init.js
 */
const uri = process.env.MONGODB_URI;
const databaseName = process.env.MONGODB_DATABASE || "utttgo";

if (!uri) {
  throw new Error("MONGODB_URI is required");
}

const database = db.getSiblingDB(databaseName);

function ensureCollection(name) {
  if (!database.getCollectionNames().includes(name)) {
    database.createCollection(name);
  }
}

function ensureIndexes(collectionName, indexes) {
  const collection = database.getCollection(collectionName);
  indexes.forEach(index => collection.createIndex(index.keys, index.options));
}

ensureCollection("products");
ensureCollection("categories");

ensureIndexes("products", [
  {
    keys: { status: 1, createdAt: -1, _id: 1 },
    options: { name: "status_createdAt_desc" }
  },
  {
    keys: { sellerId: 1, status: 1 },
    options: { name: "sellerId_status" }
  },
  {
    keys: { categoryId: 1, status: 1 },
    options: { name: "categoryId_status" }
  },
  {
    keys: { price: 1, status: 1 },
    options: { name: "price_status" }
  },
  {
    keys: { status: 1, normalizedName: 1, _id: 1 },
    options: { name: "status_normalizedName_asc" }
  }
]);

ensureIndexes("categories", [
  {
    keys: { isActive: 1, sortOrder: 1, name: 1 },
    options: { name: "isActive_sortOrder_name" }
  },
  {
    keys: { normalizedName: 1 },
    options: { name: "normalizedName_unique", unique: true }
  }
]);

print(`MongoDB initialized: ${databaseName}`);
