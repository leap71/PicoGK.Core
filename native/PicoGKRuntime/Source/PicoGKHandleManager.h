// SPDX-License-Identifier: Apache-2.0
#ifndef PICOGK_HANDLE_MANAGER_H_
#define PICOGK_HANDLE_MANAGER_H_

#include <atomic>
#include <cstdint>
#include <memory>
#include <mutex>
#include <shared_mutex>
#include <stdexcept>
#include <string>
#include <unordered_map>

namespace PicoGKHandleDetail
{
    /// Returns a process-wide unique non-zero handle value.
    inline uint64_t hNext()
    {
        static std::atomic<uint64_t> s_hNext{0};
        const uint64_t h = ++s_hNext;
        if (h == 0)
            throw std::overflow_error("PicoGK handle space exhausted");
        return h;
    }
}

/// Thread-safe owner and resolver for one native object type.
///
/// Handles are globally unique across object types and Library instances, while
/// ownership is still enforced by resolving through the owning Instance's
/// per-type manager.
template<typename T, typename Handle = uint64_t>
class PicoGKHandleManager
{
public:
    explicit PicoGKHandleManager(std::string strName)
        : m_strName(std::move(strName))
    {
    }

    Handle hAdd(std::shared_ptr<T> roObject)
    {
        if (!roObject)
            throw std::invalid_argument(m_strName + ": null object");

        const Handle h = static_cast<Handle>(PicoGKHandleDetail::hNext());
        std::unique_lock lock(m_mtx);
        m_map.emplace(h, std::move(roObject));
        return h;
    }

    std::shared_ptr<T> roGet(Handle h) const
    {
        std::shared_lock lock(m_mtx);
        auto it = m_map.find(h);
        if (it == m_map.end())
            throw std::out_of_range(m_strName + ": invalid handle " + std::to_string(h));
        return it->second;
    }

    std::shared_ptr<T> roTryGet(Handle h) const
    {
        std::shared_lock lock(m_mtx);
        auto it = m_map.find(h);
        return (it == m_map.end()) ? nullptr : it->second;
    }

    bool bDestroy(Handle h)
    {
        std::unique_lock lock(m_mtx);
        return m_map.erase(h) > 0;
    }

    bool bIsValid(Handle h) const
    {
        std::shared_lock lock(m_mtx);
        return m_map.find(h) != m_map.end();
    }

    int64_t nAllocatedCount() const
    {
        std::shared_lock lock(m_mtx);
        return static_cast<int64_t>(m_map.size());
    }

    int64_t nMemUsage() const
    {
        std::shared_lock lock(m_mtx);
        int64_t nBytes = 0;
        for (const auto& [h, roObject] : m_map)
        {
            (void)h;
            nBytes += roObject->nMemUsage();
        }
        return nBytes;
    }

private:
    std::string m_strName;
    mutable std::shared_mutex m_mtx;
    std::unordered_map<Handle, std::shared_ptr<T>> m_map;
};

#endif // PICOGK_HANDLE_MANAGER_H_
