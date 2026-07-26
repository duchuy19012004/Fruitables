(function (root, factory) {
    const api = factory();

    if (typeof module === 'object' && module.exports) {
        module.exports = api;
    } else {
        root.PriceTimelineUtils = api;
    }
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
    'use strict';

    function getTimelineEnd(schedule, fallbackEnd) {
        if (schedule.status === 'stoppedearly' && schedule.cancelledAt) {
            return new Date(schedule.cancelledAt);
        }

        if (schedule.end) {
            return new Date(schedule.end);
        }

        return fallbackEnd;
    }

    return {
        getTimelineEnd
    };
});
