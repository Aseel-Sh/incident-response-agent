export default async function teardownAiFixture() {
  try {
    await fetch('http://127.0.0.1:5199/__shutdown', { method: 'POST' });
  } catch {
    // The fixture may already be stopped after an early startup failure.
  }
}
