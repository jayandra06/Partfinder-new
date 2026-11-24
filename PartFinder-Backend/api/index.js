const { default: handler } = require('../dist/main');

/** Vercel catch-all: Nest/Express (global prefix /api). */
module.exports = async (req, res) => handler(req, res);