// Development-only, local MongoDB seeding. Run with `mongosh --nodb --file scripts/dev/seed-seller.js`.
// The connection is read from the environment, never passed on the command line or printed.
if (process.env.ASPNETCORE_ENVIRONMENT !== 'Development') {
  throw new Error('Seller seeding is available only in Development.');
}

const uri = process.env.MONGODB_URI;
if (!/^mongodb:\/\/(?:localhost|127\.0\.0\.1)(?::\d+)?(?:\/[^\s]*)?$/.test(uri || '')) {
  throw new Error('Seller seeding requires a local MongoDB URI.');
}

const name = process.env.MONGODB_DATABASE || 'utttgo';
if (!/^[A-Za-z][A-Za-z0-9_-]*$/.test(name)) {
  throw new Error('Invalid development database name.');
}

const database = connect(uri).getSiblingDB(name);
const id = ObjectId('507f1f77bcf86cd799439011');
database.sellers.updateOne({ _id: id }, {
  $set: { displayName: 'Vendedor de prueba', status: 'Active' }
}, { upsert: true });
print(`Seller seeded with id ${id.toString()}`);
