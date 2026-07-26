const assert = require('node:assert/strict');
const {
    getTimelineEnd
} = require('../wwwroot/js/price-timeline-utils.js');

const fallback = new Date('2026-08-01T00:00:00Z');

const stopped = getTimelineEnd(
    {
        status: 'stoppedearly',
        cancelledAt: '2026-07-22T03:30:00Z',
        end: '2026-07-30T03:30:00Z'
    },
    fallback);

assert.equal(
    stopped.toISOString(),
    '2026-07-22T03:30:00.000Z');

const planned = getTimelineEnd(
    {
        status: 'scheduled',
        cancelledAt: null,
        end: '2026-07-30T03:30:00Z'
    },
    fallback);

assert.equal(
    planned.toISOString(),
    '2026-07-30T03:30:00.000Z');

const openEnded = getTimelineEnd(
    {
        status: 'active',
        cancelledAt: null,
        end: null
    },
    fallback);

assert.equal(
    openEnded.toISOString(),
    fallback.toISOString());

console.log('price timeline tests passed');
